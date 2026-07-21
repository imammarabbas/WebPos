using Microsoft.Extensions.Configuration;

namespace WebPos.Client.Sdk.Security;

public sealed class ConfigurationKeyProvider(IConfiguration configuration)
    : IKeyProvider
{
    public const string PrivateKeyConfigurationKey =
        "Security:Enrollment:PrivateKeyPem";
    public const string PublicKeyConfigurationKey =
        "Security:Enrollment:PublicKeyPem";

    private readonly IConfiguration _configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    public string GetPrivateKeyPem() =>
        GetRequiredKey(PrivateKeyConfigurationKey);

    public string GetPublicKeyPem() =>
        GetRequiredKey(PublicKeyConfigurationKey);

    private string GetRequiredKey(string key)
    {
        string? value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required enrollment key '{key}' was not provided by secure configuration.");
        }

        return value.Replace("\\n", "\n", StringComparison.Ordinal);
    }
}
