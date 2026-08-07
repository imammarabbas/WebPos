namespace Common.Models;

public sealed class EnrollTerminalRequest
{
    public Guid TerminalId { get; init; }

    public string AdminUsername { get; init; } = string.Empty;

    public string AdminPassword { get; init; } = string.Empty;
}

public sealed class EnrollmentCertificateDto
{
    public string Token { get; init; } = string.Empty;

    public Guid TenantId { get; init; }

    public Guid TerminalId { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }
}

public sealed class EnrollmentPublicKeyDto
{
    public string PublicKeyPem { get; init; } = string.Empty;
}

public sealed record EnrollmentIdentity(
    Guid TenantId,
    Guid TerminalId,
    DateTimeOffset ExpiresAtUtc);
