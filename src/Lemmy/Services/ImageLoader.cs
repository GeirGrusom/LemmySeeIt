using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>
/// Downloads and decodes images, keeping a bounded in-memory cache keyed by URL and decode width.
/// Two limits matter here and both are deliberate: decoding at the size the UI will actually draw
/// (rather than full resolution), and capping how many downloads run at once so that flinging a
/// feed does not open forty sockets to the same pict-rs server.
/// </summary>
public sealed class ImageLoader : IImageLoader, IDisposable
{
    private const int MaxConcurrentDownloads = 6;
    private const int MaxCachedImages = 192;
    /// <summary>Generous enough for a full-resolution comic page, mean enough to bound the damage.</summary>
    private const int MaxImageBytes = 32 * 1024 * 1024;

    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly SemaphoreSlim downloadGate = new(MaxConcurrentDownloads, MaxConcurrentDownloads);
    private readonly ConcurrentDictionary<CacheKey, Bitmap> cache = new();
    private readonly ConcurrentQueue<CacheKey> insertionOrder = new();

    /// <summary>Creates a loader with its own transport.</summary>
    public ImageLoader(string userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);

        httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        ownsHttpClient = true;
    }

    /// <summary>Creates a loader over a transport the caller owns.</summary>
    public ImageLoader(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
        ownsHttpClient = false;
    }

    /// <inheritdoc />
    public async Task<Bitmap?> LoadAsync(WebLink link, int decodeWidth, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(decodeWidth);

        if (!link.IsValid)
        {
            return null;
        }

        var key = new CacheKey(link.Value, decodeWidth);
        if (cache.TryGetValue(key, out Bitmap? cached))
        {
            return cached;
        }

        Bitmap? decoded = await DownloadAsync(link, decodeWidth, cancellationToken).ConfigureAwait(false);
        if (decoded is null)
        {
            return null;
        }

        if (!cache.TryAdd(key, decoded))
        {
            // Another caller won the race; keep theirs so both callers share one bitmap.
            decoded.Dispose();
            return cache.TryGetValue(key, out Bitmap? winner) ? winner : null;
        }

        insertionOrder.Enqueue(key);
        Evict();

        return decoded;
    }

    /// <inheritdoc />
    public async Task<AnimatedImage?> LoadPictureAsync(WebLink link, int decodeWidth, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(decodeWidth);

        return link.IsValid ? await DownloadPictureAsync(link, decodeWidth, cancellationToken).ConfigureAwait(false) : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Same reasoning as eviction: these were handed out, and a view may still be holding one
        // while the app tears down. Letting go is enough.
        cache.Clear();
        downloadGate.Dispose();

        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }

    private async Task<Bitmap?> DownloadAsync(WebLink link, int decodeWidth, CancellationToken cancellationToken)
    {
        // Thumbnails are stills even when the picture animates: a feed of animating rows is a worse
        // feed, and the cache would be holding every frame of every one of them.
        byte[]? bytes = await FetchAsync(link, cancellationToken).ConfigureAwait(false);

        return bytes is null ? null : AnimatedImageDecoder.DecodeStillFrame(bytes, decodeWidth);
    }

    private async Task<AnimatedImage?> DownloadPictureAsync(WebLink link, int decodeWidth, CancellationToken cancellationToken)
    {
        byte[]? bytes = await FetchAsync(link, cancellationToken).ConfigureAwait(false);

        return bytes is null ? null : AnimatedImageDecoder.Decode(bytes, decodeWidth);
    }

    /// <summary>
    /// Fetches the bytes, or <see langword="null"/> if they cannot be had. Buffered rather than
    /// streamed because decoding needs more than one pass over them, and a bounded copy also caps
    /// what a server that lied about (or omitted) Content-Length can make us allocate.
    /// </summary>
    private async Task<byte[]?> FetchAsync(WebLink link, CancellationToken cancellationToken)
    {
        await downloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using HttpResponseMessage response = await httpClient
                .GetAsync(link.ToUri(), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxImageBytes)
            {
                return null;
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            using var buffer = new MemoryStream();
            await CopyBoundedAsync(body, buffer, cancellationToken).ConfigureAwait(false);

            return buffer.ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            downloadGate.Release();
        }
    }

    private static async Task CopyBoundedAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            total += read;
            if (total > MaxImageBytes)
            {
                throw new IOException("The image exceeded the size this client will decode.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Drops the oldest entries once the cache is over budget. Oldest-first rather than
    /// least-recently-used: a feed is read in one direction, so insertion order is a good enough
    /// proxy for what the reader has scrolled past.
    /// </summary>
    /// <remarks>
    /// Drops the reference; does not dispose. A cached bitmap is handed out and held for as long as
    /// the row showing it exists, and rows outlive their place in this cache — sections stay alive
    /// when the reader switches away, so browsing communities and search evicts the feed's
    /// thumbnails while the feed is still bound to them. Disposing here left an <c>Image</c>
    /// pointing at a dead bitmap, and the crash landed later, on the next layout pass, when the
    /// reader came back. Nothing references an evicted bitmap once its row is gone, and the
    /// refcounted handle beneath it has a finaliser, so the memory still comes back.
    /// </remarks>
    private void Evict()
    {
        while (cache.Count > MaxCachedImages && insertionOrder.TryDequeue(out CacheKey oldest))
        {
            cache.TryRemove(oldest, out _);
        }
    }

    private readonly record struct CacheKey(string Url, int DecodeWidth);
}
