using Microsoft.IdentityModel.Tokens;

namespace WebPos.Core.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Security:Jwt";
    public const string DefaultIssuer = "WebPos";
    public const string DefaultAudience = "WebPos.Api";

    /// <summary>Symmetric signing key (HS256). Provide via configuration/environment.</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = DefaultIssuer;

    public string Audience { get; set; } = DefaultAudience;

    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}

/// <summary>
/// Signing key resolved once at startup so token issuance and validation
/// always use the same key material.
/// </summary>
public sealed record JwtSigningKey(SymmetricSecurityKey Key);
