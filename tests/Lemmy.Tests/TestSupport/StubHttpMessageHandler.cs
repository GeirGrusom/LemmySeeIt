using System.Net;
using System.Text;

namespace Lemmy.Tests.TestSupport;

/// <summary>
/// Answers requests from a script instead of a socket, and records what was asked for. Lets the API
/// client be tested on the thing that actually goes wrong — the URL it builds and how it reads what
/// comes back — without depending on a live instance.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;
    private readonly List<Uri> requestedUris = [];

    internal StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => this.respond = respond;

    /// <summary>Answers every request with the same JSON body and status.</summary>
    internal static StubHttpMessageHandler Returning(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    /// <summary>Throws the same transport failure for every request.</summary>
    internal static StubHttpMessageHandler Failing(Exception exception) =>
        new(_ => throw exception);

    /// <summary>The Authorization header on the last request, if there was one.</summary>
    internal string? SentAuthorization { get; private set; }

    /// <summary>Every URL that was requested, in order.</summary>
    internal IReadOnlyList<Uri> RequestedUris => requestedUris;

    /// <summary>The only URL that was requested.</summary>
    internal Uri SingleRequestedUri => requestedUris.Single();

    /// <summary>Forgets what has been asked for so far.</summary>
    internal void Reset() => requestedUris.Clear();

    /// <summary>Builds a client wired to this handler.</summary>
    internal HttpClient CreateClient() => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestUri is { } uri)
        {
            requestedUris.Add(uri);
        }

        SentAuthorization = request.Headers.Authorization?.ToString();

        return Task.FromResult(respond(request));
    }
}
