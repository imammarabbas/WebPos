namespace WebPos.Client.Sdk.Security;

/// <summary>
/// Key provider that can refresh the enrollment public key at runtime
/// (e.g. when the Terminal pulls it from a Docker-hosted API).
/// </summary>
public sealed class MutableKeyProvider(IKeyProvider inner) : IKeyProvider
{
    private readonly IKeyProvider _inner =
        inner ?? throw new ArgumentNullException(nameof(inner));
    private string? _publicKeyOverride;
    private readonly object _gate = new();

    public string GetPrivateKeyPem() => _inner.GetPrivateKeyPem();

    public string GetPublicKeyPem()
    {
        lock (_gate)
        {
            return string.IsNullOrWhiteSpace(_publicKeyOverride)
                ? _inner.GetPublicKeyPem()
                : _publicKeyOverride;
        }
    }

    public void SetPublicKeyPem(string publicKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        lock (_gate)
        {
            _publicKeyOverride = publicKeyPem.Replace(
                "\\n",
                "\n",
                StringComparison.Ordinal);
        }
    }
}
