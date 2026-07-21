namespace WebPos.Client.Sdk.Security;

public interface IKeyProvider
{
    string GetPrivateKeyPem();

    string GetPublicKeyPem();
}
