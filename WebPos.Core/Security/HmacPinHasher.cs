using System.Security.Cryptography;
using System.Text;

namespace WebPos.Core.Security;

public sealed class HmacPinHasher : IPinHasher
{
    public const string ConfigurationKey = "Security:PinHashKey";
    private const int MinimumKeyLengthBytes = 32;

    private readonly byte[] _key;

    public HmacPinHasher(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? encodedKey = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            throw new InvalidOperationException(
                $"Missing PIN hashing key. Configure '{ConfigurationKey}' with a Base64-encoded secret.");
        }

        try
        {
            _key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"'{ConfigurationKey}' must be a valid Base64-encoded secret.",
                ex);
        }

        if (_key.Length < MinimumKeyLengthBytes)
        {
            throw new InvalidOperationException(
                $"'{ConfigurationKey}' must decode to at least {MinimumKeyLengthBytes} bytes.");
        }
    }

    public string HashPin(string pin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);

        byte[] hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(pin));
        return Convert.ToHexString(hash);
    }

    public bool VerifyPin(string pin, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(pin) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string computedHash = HashPin(pin);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedHash),
            Encoding.ASCII.GetBytes(storedHash));
    }
}
