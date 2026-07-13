using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using WebPos.Configuration;

namespace WebPos.Filters;

/// <summary>
/// Requires every controller action to send a matching <c>X-Api-Version</c> header.
/// </summary>
public sealed class ApiVersionFilter : IAsyncActionFilter
{
    public const string HeaderName = "X-Api-Version";

    private readonly string _expectedVersion;

    public ApiVersionFilter(IOptions<ApiVersionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _expectedVersion = string.IsNullOrWhiteSpace(options.Value.Version)
            ? "1.0.0"
            : options.Value.Version.Trim();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues)
            || string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            context.Result = CreateUpgradeRequiredResult(
                $"Missing required '{HeaderName}' header. Expected version '{_expectedVersion}'.");
            return;
        }

        string providedVersion = headerValues.ToString().Trim();
        if (!string.Equals(providedVersion, _expectedVersion, StringComparison.Ordinal))
        {
            context.Result = CreateUpgradeRequiredResult(
                $"Unsupported API version '{providedVersion}'. Expected '{_expectedVersion}'.");
            return;
        }

        await next();
    }

    private static ObjectResult CreateUpgradeRequiredResult(string message) =>
        new(new { error = message })
        {
            StatusCode = StatusCodes.Status426UpgradeRequired
        };
}
