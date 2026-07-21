namespace WebPos.Client.Sdk;

public sealed class ApiClientOptions
{
    public const string SectionName = "WebPosSdk";

    /// <summary>Base URL of the WebPos API (e.g. https://localhost:8285/).</summary>
    public string BaseAddress { get; set; } = "https://localhost:8285/";

    /// <summary>Value sent on every request as X-Api-Version.</summary>
    public string ApiVersion { get; set; } = "1.0.0";
}
