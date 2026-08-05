using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using WebPos.Core.Services;

namespace WebPos.Core.Security;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/account/complete-login", (Delegate)CompleteLoginAsync);
        endpoints.MapGet("/account/logout", (Delegate)LogoutAsync);
        return endpoints;
    }

    private static async Task<IResult> CompleteLoginAsync(
        HttpContext httpContext,
        LoginTicketStore tickets,
        string? ticket)
    {
        if (!tickets.TryRedeem(ticket, out LoginTicketPayload payload))
        {
            return Results.Redirect("/login");
        }

        ClaimsPrincipal principal = CreatePrincipal(payload);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true
            });

        return Results.Redirect("/master");
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }

    public static ClaimsPrincipal CreatePrincipal(LoginTicketPayload payload)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, payload.UserId.ToString()),
            new(ClaimTypes.Name, payload.Username),
            new(ClaimTypes.Role, payload.RoleName),
            new(HttpContextTenantService.JwtTenantClaimType, payload.TenantId.ToString())
        ];

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
