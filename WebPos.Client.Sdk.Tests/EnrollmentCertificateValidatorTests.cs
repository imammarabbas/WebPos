using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using WebPos.Client.Sdk.Security;

namespace WebPos.Client.Sdk.Tests;

public sealed class EnrollmentCertificateValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnSignedTenantAndTerminalIdentity()
    {
        using RSA rsa = RSA.Create(3072);
        var keyProvider = new TestKeyProvider(
            rsa.ExportPkcs8PrivateKeyPem(),
            rsa.ExportSubjectPublicKeyInfoPem());
        var validator = new EnrollmentCertificateValidator(keyProvider);
        Guid tenantId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        string token = CreateToken(rsa, tenantId, terminalId);

        Common.Models.EnrollmentIdentity identity = validator.Validate(token);

        identity.TenantId.Should().Be(tenantId);
        identity.TerminalId.Should().Be(terminalId);
        identity.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Validate_ShouldRejectCertificateSignedByDifferentKey()
    {
        using RSA trustedRsa = RSA.Create(3072);
        using RSA attackerRsa = RSA.Create(3072);
        var validator = new EnrollmentCertificateValidator(
            new TestKeyProvider(
                trustedRsa.ExportPkcs8PrivateKeyPem(),
                trustedRsa.ExportSubjectPublicKeyInfoPem()));
        string token = CreateToken(
            attackerRsa,
            Guid.NewGuid(),
            Guid.NewGuid());

        Action act = () => validator.Validate(token);

        act.Should().Throw<SecurityTokenInvalidSignatureException>();
    }

    private static string CreateToken(
        RSA rsa,
        Guid tenantId,
        Guid terminalId)
    {
        Claim[] claims =
        [
            new(
                EnrollmentCertificateValidator.TenantClaimType,
                tenantId.ToString()),
            new(
                EnrollmentCertificateValidator.TerminalClaimType,
                terminalId.ToString()),
            new(
                EnrollmentCertificateValidator.CertificateTypeClaim,
                EnrollmentCertificateValidator.EnrollmentCertificateType)
        ];
        var token = new JwtSecurityToken(
            EnrollmentCertificateValidator.Issuer,
            EnrollmentCertificateValidator.Audience,
            claims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddHours(1),
            new SigningCredentials(
                new RsaSecurityKey(rsa),
                SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TestKeyProvider(
        string privateKey,
        string publicKey) : IKeyProvider
    {
        public string GetPrivateKeyPem() => privateKey;

        public string GetPublicKeyPem() => publicKey;
    }
}
