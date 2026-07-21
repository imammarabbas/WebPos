using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WebPos.Client.Sdk.Security;

namespace WebPos.Middleware;

public sealed class EnrollmentCertificateMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next =
        next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(
        HttpContext context,
        IEnrollmentCertificateValidator validator)
    {
        string authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            if (context.Request.Path.StartsWithSegments("/api")
                && !AllowsAnonymousApiAccess(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await _next(context);
            return;
        }

        string token = authorization["Bearer ".Length..].Trim();
        try
        {
            Common.Models.EnrollmentIdentity identity = validator.Validate(token);
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new Claim(
                        EnrollmentCertificateValidator.TenantClaimType,
                        identity.TenantId.ToString()),
                    new Claim(
                        EnrollmentCertificateValidator.TerminalClaimType,
                        identity.TerminalId.ToString()),
                    new Claim(
                        EnrollmentCertificateValidator.CertificateTypeClaim,
                        EnrollmentCertificateValidator.EnrollmentCertificateType)
                ],
                EnrollmentCertificateValidator.AuthenticationType));
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            // Not an enrollment certificate — fall back to user JWT (JwtBearer).
            AuthenticateResult result = await context.AuthenticateAsync(
                JwtBearerDefaults.AuthenticationScheme);
            if (result is { Succeeded: true, Principal: not null })
            {
                context.User = result.Principal;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Entry points that do not require a Bearer token at the middleware layer.
    /// All other /api routes require enrollment cert or user JWT.
    /// </summary>
    private static bool AllowsAnonymousApiAccess(PathString path) =>
        path.StartsWithSegments("/api/terminal-enrollment")
        || path.StartsWithSegments("/api/auth/login");
}
