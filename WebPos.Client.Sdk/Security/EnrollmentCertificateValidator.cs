using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Common.Models;
using Microsoft.IdentityModel.Tokens;

namespace WebPos.Client.Sdk.Security;

public interface IEnrollmentCertificateValidator
{
    EnrollmentIdentity Validate(string token);
}

public sealed class EnrollmentCertificateValidator(IKeyProvider keyProvider)
    : IEnrollmentCertificateValidator
{
    public const string Issuer = "WebPos";
    public const string Audience = "WebPos.Terminal";
    public const string TenantClaimType = "tenant_id";
    public const string TerminalClaimType = "terminal_id";
    public const string CertificateTypeClaim = "certificate_type";
    public const string EnrollmentCertificateType = "terminal_enrollment";
    public const string AuthenticationType = "EnrollmentCertificate";

    private readonly IKeyProvider _keyProvider =
        keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));

    public EnrollmentIdentity Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new SecurityTokenException("Enrollment certificate is missing.");
        }

        using RSA rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(_keyProvider.GetPublicKeyPem());
        }
        catch (Exception exception)
            when (exception is ArgumentException or CryptographicException)
        {
            throw new SecurityTokenException(
                "The configured enrollment public key is invalid.",
                exception);
        }

        RsaSecurityKey signingKey = new(rsa.ExportParameters(includePrivateParameters: false));

        JwtSecurityTokenHandler handler = new()
        {
            MapInboundClaims = false
        };
        TokenValidationParameters validationParameters = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        ClaimsPrincipal principal = handler.ValidateToken(
            token,
            validationParameters,
            out SecurityToken validatedToken);
        if (validatedToken is not JwtSecurityToken jwt
            || !string.Equals(
                jwt.Header.Alg,
                SecurityAlgorithms.RsaSha256,
                StringComparison.Ordinal))
        {
            throw new SecurityTokenException(
                "Enrollment certificate does not use the required RS256 algorithm.");
        }

        string? certificateType =
            principal.FindFirst(CertificateTypeClaim)?.Value;
        if (!string.Equals(
                certificateType,
                EnrollmentCertificateType,
                StringComparison.Ordinal))
        {
            throw new SecurityTokenException(
                "Token is not a terminal enrollment certificate.");
        }

        if (!Guid.TryParse(
                principal.FindFirst(TenantClaimType)?.Value,
                out Guid tenantId)
            || tenantId == Guid.Empty
            || !Guid.TryParse(
                principal.FindFirst(TerminalClaimType)?.Value,
                out Guid terminalId)
            || terminalId == Guid.Empty)
        {
            throw new SecurityTokenException(
                "Enrollment certificate identity claims are invalid.");
        }

        return new EnrollmentIdentity(
            tenantId,
            terminalId,
            new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
    }
}
