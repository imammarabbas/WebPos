using System.Net;

namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// UI-safe outcome of an API call. Components consume this instead of catching SDK exceptions.
/// </summary>
public sealed class Result<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public HttpStatusCode? StatusCode { get; init; }

    public static Result<T> Ok(T data) => new()
    {
        Success = true,
        Data = data,
        ErrorMessage = string.Empty
    };

    public static Result<T> Fail(string errorMessage, HttpStatusCode? statusCode = null) => new()
    {
        Success = false,
        Data = default,
        ErrorMessage = errorMessage,
        StatusCode = statusCode
    };
}
