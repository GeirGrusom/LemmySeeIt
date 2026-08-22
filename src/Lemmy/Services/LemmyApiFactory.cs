using Lemmy.Api;
using Lemmy.Domain;

namespace Lemmy.Services;

/// <summary>
/// Builds clients over one shared <see cref="HttpClient"/>. Sharing the transport is what keeps
/// connections pooled across instance switches, which matters on mobile where the handshake costs
/// more than the request.
/// </summary>
public sealed class LemmyApiFactory : ILemmyApiFactory, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;

    /// <summary>Creates a factory with its own transport.</summary>
    /// <param name="userAgent">Sent on every request; instances rate-limit anonymous clients by it.</param>
    public LemmyApiFactory(string userAgent)
    {
        httpClient = LemmyApiClient.CreateHttpClient(userAgent);
        ownsHttpClient = true;
    }

    /// <summary>Creates a factory over a transport the caller owns.</summary>
    public LemmyApiFactory(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        this.httpClient = httpClient;
        ownsHttpClient = false;
    }

    /// <inheritdoc />
    public ILemmyApi Create(InstanceAddress instance, SessionToken session = default) =>
        new LemmyApiClient(httpClient, instance, session);

    /// <inheritdoc />
    public void Dispose()
    {
        if (ownsHttpClient)
        {
            httpClient.Dispose();
        }
    }
}
