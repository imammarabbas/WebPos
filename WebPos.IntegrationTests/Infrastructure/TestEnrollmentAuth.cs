using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using WebPos.Client.Sdk.Security;
using WebPos.Core.Entities;

namespace WebPos.IntegrationTests.Infrastructure;

/// <summary>
/// Generates enrollment Bearer tokens for API integration tests.
/// </summary>
public static class TestEnrollmentAuth
{
    public const string PrivateKeyPemConfigKey = "Security:Enrollment:PrivateKeyPem";
    public const string PublicKeyPemConfigKey = "Security:Enrollment:PublicKeyPem";

    /// <summary>3072-bit RSA key pair for test factories (generated once per process).</summary>
    public static (string PrivateKeyPem, string PublicKeyPem) GenerateRsaKeyPair()
    {
        using RSA rsa = RSA.Create(3072);
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
    }

    public static string CreateEnrollmentBearerToken(
        Guid tenantId,
        Guid terminalId,
        string privateKeyPem,
        TimeSpan? lifetime = null)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        RsaSecurityKey signingKey = new(rsa.ExportParameters(includePrivateParameters: true));

        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = issuedAt.Add(lifetime ?? TimeSpan.FromDays(1));

        Claim[] claims =
        [
            new(EnrollmentCertificateValidator.TenantClaimType, tenantId.ToString()),
            new(EnrollmentCertificateValidator.TerminalClaimType, terminalId.ToString()),
            new(
                EnrollmentCertificateValidator.CertificateTypeClaim,
                EnrollmentCertificateValidator.EnrollmentCertificateType),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        var token = new JwtSecurityToken(
            issuer: EnrollmentCertificateValidator.Issuer,
            audience: EnrollmentCertificateValidator.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void ApplyEnrollmentAuth(
        HttpRequestMessage request,
        Guid tenantId,
        Guid terminalId,
        string privateKeyPem)
    {
        string token = CreateEnrollmentBearerToken(tenantId, terminalId, privateKeyPem);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public static Guid DefaultTenantId => TenantDefaults.MasterTenantId;
}
