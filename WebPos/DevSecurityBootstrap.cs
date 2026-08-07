using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WebPos;

/// <summary>
/// Local-only security defaults so a restored DB + new machine can enroll without Generate-PilotEnv.
/// </summary>
internal static class DevSecurityBootstrap
{
    private const int EnrollmentRsaBits = 3072;

    public static void Apply(WebApplicationBuilder builder)
    {
        bool allow =
            builder.Environment.IsDevelopment()
            || string.Equals(
                builder.Configuration["Security:AllowInsecureDevDefaults"],
                "true",
                StringComparison.OrdinalIgnoreCase);

        if (!allow || builder.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        IConfiguration config = builder.Configuration;
        bool injected = false;
        string secretsDir = Path.Combine(builder.Environment.ContentRootPath, "dev-secrets");
        Directory.CreateDirectory(secretsDir);

        if (string.IsNullOrWhiteSpace(config["Security:PinHashKey"]))
        {
            byte[] pinKey = SHA256.HashData("WebPos.Dev.PinHashKey.v1"u8.ToArray());
            config["Security:PinHashKey"] = Convert.ToBase64String(pinKey);
            injected = true;
        }

        if (string.IsNullOrWhiteSpace(config["Security:Jwt:Key"]))
        {
            config["Security:Jwt:Key"] = "dev_jwt_secret_key_must_be_32_chars_min";
            injected = true;
        }

        string privatePath = Path.Combine(secretsDir, "enrollment-private.pem");
        string publicPath = Path.Combine(secretsDir, "enrollment-public.pem");

        bool missingPrivate = string.IsNullOrWhiteSpace(config["Security:Enrollment:PrivateKeyPem"]);
        bool missingPublic = string.IsNullOrWhiteSpace(config["Security:Enrollment:PublicKeyPem"]);

        if (missingPrivate || missingPublic)
        {
            if (File.Exists(privatePath) && File.Exists(publicPath))
            {
                config["Security:Enrollment:PrivateKeyPem"] = File.ReadAllText(privatePath);
                config["Security:Enrollment:PublicKeyPem"] = File.ReadAllText(publicPath);
                injected = true;
            }
            else
            {
                using RSA rsa = RSA.Create(EnrollmentRsaBits);
                string privatePem = rsa.ExportPkcs8PrivateKeyPem()
                    .Replace("\r\n", "\n", StringComparison.Ordinal);
                string publicPem = rsa.ExportSubjectPublicKeyInfoPem()
                    .Replace("\r\n", "\n", StringComparison.Ordinal);

                File.WriteAllText(privatePath, privatePem);
                File.WriteAllText(publicPath, publicPem);

                config["Security:Enrollment:PrivateKeyPem"] = privatePem;
                config["Security:Enrollment:PublicKeyPem"] = publicPem;
                injected = true;
            }
        }

        string? publicPemConfig = config["Security:Enrollment:PublicKeyPem"];
        if (!string.IsNullOrWhiteSpace(publicPemConfig))
        {
            TrySyncTerminalEnrollmentPublicKey(
                builder.Environment.ContentRootPath,
                publicPemConfig.Replace("\\n", "\n", StringComparison.Ordinal));
        }

        if (injected)
        {
            Console.WriteLine(
                "WARNING: Security:* local defaults active (dev-secrets/). " +
                "Do not use in production. Enroll with admin / admin123 after startup seed.");
        }
    }

    /// <summary>
    /// Keeps Windows Terminal's enrollment public key aligned with this API's signing key.
    /// </summary>
    private static void TrySyncTerminalEnrollmentPublicKey(string apiContentRoot, string publicPem)
    {
        try
        {
            string? repoRoot = Directory.GetParent(apiContentRoot)?.FullName;
            if (repoRoot is null)
            {
                return;
            }

            string terminalSettings = Path.Combine(
                repoRoot,
                "WebPos.WindowsTerminal",
                "appsettings.json");

            if (!File.Exists(terminalSettings))
            {
                return;
            }

            JsonNode? root = JsonNode.Parse(File.ReadAllText(terminalSettings)) ?? new JsonObject();
            JsonObject security = root["Security"] as JsonObject ?? new JsonObject();
            JsonObject enrollment = security["Enrollment"] as JsonObject ?? new JsonObject();
            enrollment["PublicKeyPem"] = publicPem;
            security["Enrollment"] = enrollment;
            root["Security"] = security;

            if (root["WebPosSdk"] is null)
            {
                root["WebPosSdk"] = new JsonObject
                {
                    ["BaseAddress"] = "http://localhost:8080/",
                    ["ApiVersion"] = "1.0.0"
                };
            }

            File.WriteAllText(
                terminalSettings,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"WARNING: Could not sync enrollment public key to Windows Terminal ({ex.Message}).");
        }
    }
}
