namespace WebPos.Core.Abstractions;

public sealed class LoginRequest
{
    public required string Username { get; init; }

    public required string Password { get; init; }
}

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public string TokenType { get; init; } = "Bearer";

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required string Username { get; init; }

    public required string Role { get; init; }
}

public interface ISecurityService
{
    /// <summary>
    /// Verifies credentials for the current tenant and issues a signed JWT.
    /// Returns null when authentication fails.
    /// </summary>
    Task<AuthResponse?> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
