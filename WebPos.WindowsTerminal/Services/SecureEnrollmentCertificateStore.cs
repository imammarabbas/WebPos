using Common.Models;
using WebPos.Client.Sdk.Security;

namespace WebPos.WindowsTerminal.Services;

public sealed class SecureEnrollmentCertificateStore(
    IEnrollmentCertificateValidator validator)
    : IEnrollmentCertificateAccessor
{
    private const string SecureStorageKey = "webpos.enrollment.certificate";

    private readonly IEnrollmentCertificateValidator _validator =
        validator ?? throw new ArgumentNullException(nameof(validator));

    public string? Token { get; private set; }

    public EnrollmentIdentity? Identity { get; private set; }

    public bool IsEnrolled => Identity is not null && !string.IsNullOrWhiteSpace(Token);

    /// <summary>
    /// Loads a previously stored certificate. Missing cert is allowed (show enroll UI).
    /// </summary>
    public async Task InitializeAsync()
    {
        string? token = await SecureStorage.Default.GetAsync(SecureStorageKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            Token = null;
            Identity = null;
            return;
        }

        try
        {
            EnrollmentIdentity identity = _validator.Validate(token);
            Token = token;
            Identity = identity;
        }
        catch
        {
            SecureStorage.Default.Remove(SecureStorageKey);
            Token = null;
            Identity = null;
        }
    }

    public async Task StoreAsync(string token)
    {
        EnrollmentIdentity identity = _validator.Validate(token);
        await SecureStorage.Default.SetAsync(SecureStorageKey, token);
        Token = token;
        Identity = identity;
    }

    public void Remove()
    {
        SecureStorage.Default.Remove(SecureStorageKey);
        Token = null;
        Identity = null;
    }
}
