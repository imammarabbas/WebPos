using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using WebPos.Core.Models;

namespace WebPos.Core.Security;

public class PosAuthStateProvider : AuthenticationStateProvider
{
    private const string UserIdKey = "userId";
    private const string UserNameKey = "userName";
    private const string UserRoleKey = "userRole";

    private readonly ProtectedSessionStorage _sessionStorage;
    private static readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public PosAuthStateProvider(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage ?? throw new ArgumentNullException(nameof(sessionStorage));
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            ProtectedBrowserStorageResult<string> result = await _sessionStorage.GetAsync<string>(UserIdKey);
            if (!result.Success || string.IsNullOrEmpty(result.Value))
            {
                return new AuthenticationState(_anonymous);
            }

            ProtectedBrowserStorageResult<string> roleResult = await _sessionStorage.GetAsync<string>(UserRoleKey);
            ProtectedBrowserStorageResult<string> nameResult = await _sessionStorage.GetAsync<string>(UserNameKey);

            ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, result.Value),
                new Claim(ClaimTypes.Name, nameResult.Value ?? "User"),
                new Claim(ClaimTypes.Role, roleResult.Value ?? "Cashier")
            ],
            "CustomAuth");

            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (InvalidOperationException)
        {
            return new AuthenticationState(_anonymous);
        }
        catch
        {
            return new AuthenticationState(_anonymous);
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
        }
        catch (InvalidOperationException)
        {
            // Session storage is unavailable during prerender.
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
