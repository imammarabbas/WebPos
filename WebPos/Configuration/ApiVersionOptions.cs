namespace WebPos.Configuration;

public sealed class ApiVersionOptions
{
    public const string SectionName = "Api";

    /// <summary>Expected value of the X-Api-Version request header.</summary>
    public string Version { get; set; } = "1.0.0";
}
