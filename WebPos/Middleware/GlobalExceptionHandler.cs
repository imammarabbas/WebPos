using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebPos.Core.Services;

namespace WebPos.Middleware;

/// <summary>
/// Maps domain/business failures to stable API error responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int statusCode, string title, string detail) = exception switch
        {
            ValidationException validation =>
                (StatusCodes.Status400BadRequest,
                    "Validation Failed",
                    string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))),
            UnauthorizedAccessException =>
                (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            KeyNotFoundException notFound =>
                (StatusCodes.Status404NotFound, "Not Found", notFound.Message),
            ShiftAuthorizationException authorization =>
                (StatusCodes.Status403Forbidden, "Forbidden", authorization.Message),
            ShiftConflictException conflict =>
                (StatusCodes.Status409Conflict, "Conflict", conflict.Message),
            InvalidOperationException invalidOperation =>
                (StatusCodes.Status400BadRequest, "Bad Request", invalidOperation.Message),
            _ => default
        };

        if (statusCode == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        ProblemDetails problem = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
