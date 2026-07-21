using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebPos.Core.Interfaces;
using WebPos.Core.Security;
using WebPos.Core.Services;

namespace WebPos.Filters;

/// <summary>
/// Central tenant-scoped authorization. Requires an authenticated caller whose
/// tenant claim (tid from a user JWT, or tenant_id from an enrollment certificate)
/// matches both the tenantId route value (when present) and the tenant resolved
/// by <see cref="ITenantService"/>. Mismatches return 403 Forbidden.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class TenantAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
{
    private const string TenantRouteKey = "tenantId";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ClaimsPrincipal user = context.HttpContext.User;
        if (user.Identity is not { IsAuthenticated: true })
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        string? claimValue =
            user.FindFirst(JwtService.TenantClaimType)?.Value
            ?? user.FindFirst(HttpContextTenantService.TenantClaimType)?.Value;

        if (!Guid.TryParse(claimValue, out Guid tokenTenantId) || tokenTenantId == Guid.Empty)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        // Route-level check: /api/{tenantId}/... style routes must match the token.
        if (context.RouteData.Values.TryGetValue(TenantRouteKey, out object? routeValue)
            && routeValue is not null)
        {
            if (!Guid.TryParse(routeValue.ToString(), out Guid routeTenantId)
                || routeTenantId != tokenTenantId)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }
        }

        // ITenantService remains the source of truth for request processing;
        // a token for a different tenant than the resolved context is rejected.
        ITenantService tenantService = context.HttpContext.RequestServices
            .GetRequiredService<ITenantService>();
        if (tenantService.IsResolved && tenantService.TenantId != tokenTenantId)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}
