using System.Net;

namespace WebPos.Client.Sdk.Exceptions;

/// <summary>
/// Thrown when the WebPos API returns a non-success HTTP status.
/// </summary>
public sealed class WebPosClientException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public string? ResponseBody { get; }

    public string? RequestPath { get; }

    public WebPosClientException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody = null,
        string? requestPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        RequestPath = requestPath;
    }
}

/// <summary>
/// Thrown when an API response cannot be deserialized into the expected model.
/// </summary>
public sealed class ApiDeserializationException : Exception
{
    public string? ResponseBody { get; }

    public Type TargetType { get; }

    public ApiDeserializationException(
        Type targetType,
        string message,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        TargetType = targetType;
        ResponseBody = responseBody;
    }
}
