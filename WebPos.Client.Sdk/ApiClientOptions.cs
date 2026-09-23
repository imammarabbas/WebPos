namespace WebPos.Client.Sdk;

public sealed class ApiClientOptions
{
    public const string SectionName = "WebPosSdk";

    /// <summary>
    /// Base URL of the WebPos API. Matches the host <c>http</c> launch profile and Docker publish port.
    /// </summary>
    public string BaseAddress { get; set; } = "http://localhost:8080/";

    /// <summary>Value sent on every request as X-Api-Version.</summary>
    public string ApiVersion { get; set; } = "1.0.0";

    /// <summary>
    /// HttpClient timeout in seconds. Keep short so Offline / login fail fast when the API is down.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;
}
