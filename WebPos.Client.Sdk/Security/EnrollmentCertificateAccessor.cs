using Common.Models;

namespace WebPos.Client.Sdk.Security;

public interface IEnrollmentCertificateAccessor
{
    string? Token { get; }

    EnrollmentIdentity? Identity { get; }
}

internal sealed class EmptyEnrollmentCertificateAccessor
    : IEnrollmentCertificateAccessor
{
    public string? Token => null;

    public EnrollmentIdentity? Identity => null;
}
