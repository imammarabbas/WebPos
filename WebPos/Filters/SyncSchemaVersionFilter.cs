using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebPos.Core.Services;

namespace WebPos.Filters;

/// <summary>
/// Stamps X-Sync-Schema-Version on every sync response and rejects clients
/// that declare an incompatible schema version with 406 Not Acceptable.
/// Compatibility rule: major version must match (e.g. client "1.x" vs server "1.0").
/// </summary>
public sealed class SyncSchemaVersionFilter : IActionFilter
{
    public const string HeaderName = "X-Sync-Schema-Version";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.HttpContext.Response.Headers[HeaderName] = SyncService.SchemaVersion;

        if (!context.HttpContext.Request.Headers.TryGetValue(
                HeaderName, out var headerValues))
        {
            return; // header optional: absent client version is treated as current
        }

        string clientVersion = headerValues.ToString().Trim();
        if (clientVersion.Length == 0 || IsCompatible(clientVersion))
        {
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status406NotAcceptable,
            Title = "Sync Schema Version Not Acceptable",
            Detail =
                $"Client sync schema '{clientVersion}' is incompatible with "
                + $"server schema '{SyncService.SchemaVersion}'. Update the client."
        })
        {
            StatusCode = StatusCodes.Status406NotAcceptable
        };
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static bool IsCompatible(string clientVersion)
    {
        string serverMajor = SyncService.SchemaVersion.Split('.')[0];
        string clientMajor = clientVersion.Split('.')[0];
        return string.Equals(serverMajor, clientMajor, StringComparison.Ordinal);
    }
}
