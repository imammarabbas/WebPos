using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Http;
using WebPos.Core.Models;
using WebPos.Core.Services;

namespace WebPos.Core.Security;

public class PosAuthStateProvider : AuthenticationStateProvider
{
    private const string UserIdKey = "userId";
    private const string UserNameKey = "userName";
    private const string UserRoleKey = "userRole";
    private const string UserTenantKey = "userTenantId";

    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public PosAuthStateProvider(
        ProtectedSessionStorage sessionStorage,
        IHttpContextAccessor httpContextAccessor)
    {
        _sessionStorage = sessionStorage ?? throw new ArgumentNullException(nameof(sessionStorage));
        _httpContextAccessor = httpContextAccessor
            ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsPrincipal? cookieUser = _httpContextAccessor.HttpContext?.User;
        if (cookieUser?.Identity?.IsAuthenticated == true)
        {
            return new AuthenticationState(cookieUser);
        }

        try
        {
            ProtectedBrowserStorageResult<string> result = await _sessionStorage.GetAsync<string>(UserIdKey);
            if (!result.Success || string.IsNullOrEmpty(result.Value))
            {
                return new AuthenticationState(Anonymous);
            }

            ProtectedBrowserStorageResult<string> roleResult = await _sessionStorage.GetAsync<string>(UserRoleKey);
            ProtectedBrowserStorageResult<string> nameResult = await _sessionStorage.GetAsync<string>(UserNameKey);
            ProtectedBrowserStorageResult<string> tenantResult = await _sessionStorage.GetAsync<string>(UserTenantKey);

            List<Claim> claims =
            [
                new Claim(ClaimTypes.NameIdentifier, result.Value),
                new Claim(ClaimTypes.Name, nameResult.Value ?? "User"),
                new Claim(ClaimTypes.Role, roleResult.Value ?? "Cashier")
            ];

            if (!string.IsNullOrWhiteSpace(tenantResult.Value))
            {
                claims.Add(new Claim(HttpContextTenantService.JwtTenantClaimType, tenantResult.Value));
            }

            ClaimsIdentity identity = new(claims, "CustomAuth");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (InvalidOperationException)
        {
            return new AuthenticationState(Anonymous);
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }

    public async Task MarkUserAsAuthenticated(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(user.Role);

        try
        {
            await _sessionStorage.SetAsync(UserIdKey, user.Id.ToString());
            await _sessionStorage.SetAsync(UserNameKey, user.Username);
            await _sessionStorage.SetAsync(UserRoleKey, user.Role.RoleName);
            await _sessionStorage.SetAsync(UserTenantKey, user.TenantId.ToString());
        }
        catch (InvalidOperationException)
        {
            // Session storage is unavailable during prerender; cookie auth still applies.
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task MarkUserAsLoggedOut()
    {
        try
        {
            await _sessionStorage.DeleteAsync(UserIdKey);
            await _sessionStorage.DeleteAsync(UserNameKey);
            await _sessionStorage.DeleteAsync(UserRoleKey);
            await _sessionStorage.DeleteAsync(UserTenantKey);
        }
        catch (InvalidOperationException)
        {
            // Session storage is unavailable during prerender.
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
