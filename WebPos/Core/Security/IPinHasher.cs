namespace WebPos.Core.Security;

public interface IPinHasher
{
    string HashPin(string pin);

    bool VerifyPin(string pin, string storedHash);
}
