using System.Net;

namespace Lemmy.Api;

/// <summary>Thrown when a Lemmy server refuses a request or answers with something unusable.</summary>
public sealed class LemmyApiException : Exception
{
    /// <summary>Creates an exception with no further detail.</summary>
    public LemmyApiException()
        : base("The Lemmy server did not answer the request.")
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    public LemmyApiException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and a cause.</summary>
    public LemmyApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an exception describing a specific failed endpoint.</summary>
    public LemmyApiException(string message, string endpoint, HttpStatusCode? statusCode, string? serverError = null)
        : base(message)
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
        ServerError = serverError;
    }

    /// <summary>The API path that failed, e.g. <c>api/v3/post/list</c>.</summary>
    public string? Endpoint { get; }

    /// <summary>The HTTP status, when the request got far enough to have one.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Lemmy's own error code, e.g. <c>couldnt_find_post</c>, when it sent one.</summary>
    public string? ServerError { get; }

    /// <summary>Whether retrying the same request has a reasonable chance of working.</summary>
    public bool IsTransient => StatusCode switch
    {
        null => true,
        HttpStatusCode.RequestTimeout => true,
        HttpStatusCode.TooManyRequests => true,
        HttpStatusCode.InternalServerError => true,
        HttpStatusCode.BadGateway => true,
        HttpStatusCode.ServiceUnavailable => true,
        HttpStatusCode.GatewayTimeout => true,
        _ => false,
    };
}
