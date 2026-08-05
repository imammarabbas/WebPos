# Starts WebPos API locally using .env (no secret echo). Falls back when Docker Hub is unavailable.
# Always rebuilds when sources are newer than WebPos.dll, and stops any prior pilot PID on :8080.
# Usage: powershell -File scripts\Start-PilotApi.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$envFile = Join-Path $root ".env"
if (-not (Test-Path $envFile)) {
    throw "Missing .env — run scripts\Generate-PilotEnv.ps1 first."
}

$genDir = Join-Path $env:TEMP "WebPosPilotApiStart"
New-Item -ItemType Directory -Force -Path $genDir | Out-Null

@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
'@ | Set-Content (Join-Path $genDir "Start.csproj") -Encoding utf8

@'
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

string root = args[0];
string envPath = Path.Combine(root, ".env");
var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (string line in File.ReadAllLines(envPath))
{
    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
    int i = line.IndexOf('=');
    if (i <= 0) continue;
    map[line[..i].Trim()] = line[(i + 1)..];
}

string password = map.GetValueOrDefault("POSTGRES_PASSWORD")
    ?? Environment.GetEnvironmentVariable("LOCAL_POSTGRES_PASSWORD")
    ?? "sa";
// Same DB name as docker-compose so Terminal (8080) and Master share one database.
string conn =
    $"Server=localhost;Port=5432;Database=WebPos;User Id=postgres;Password={password};";

string projectPath = Path.Combine(root, "WebPos", "WebPos.csproj");
string dll = Path.Combine(root, "WebPos", "bin", "Debug", "net10.0", "WebPos.dll");
string webPosDir = Path.Combine(root, "WebPos");

bool needsBuild = !File.Exists(dll);
if (!needsBuild)
{
    DateTime dllWrite = File.GetLastWriteTimeUtc(dll);
    needsBuild = Directory.EnumerateFiles(webPosDir, "*.cs", SearchOption.AllDirectories)
        .Concat(Directory.EnumerateFiles(webPosDir, "*.csproj", SearchOption.TopDirectoryOnly))
        .Any(path => File.GetLastWriteTimeUtc(path) > dllWrite);
}

if (needsBuild)
{
    Console.Error.WriteLine("Building WebPos (sources newer than DLL or DLL missing)...");
    var build = Process.Start(new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"build \"{projectPath}\" -c Debug -v q",
        WorkingDirectory = root,
        UseShellExecute = false
    })!;
    build.WaitForExit();
    if (build.ExitCode != 0) Environment.Exit(build.ExitCode);
}
else
{
    Console.WriteLine("WebPos.dll is up to date.");
}

string pidFile = Path.Combine(root, "scripts", ".pilot-api.pid");
StopExistingPilot(pidFile);

// Free :8080 when a prior orphaned process still holds it.
TryStopListenerOnPort(8080);

var psi = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = $"\"{dll}\"",
    WorkingDirectory = Path.Combine(root, "WebPos", "bin", "Debug", "net10.0"),
    UseShellExecute = false
};
psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
psi.Environment["ASPNETCORE_URLS"] = "http://localhost:8080";
psi.Environment["ConnectionStrings__DefaultConnection"] = conn;
psi.Environment["Security__PinHashKey"] = map["SECURITY_PIN_HASH_KEY"];
psi.Environment["Security__Jwt__Key"] = map["SECURITY_JWT_KEY"];
psi.Environment["Security__Enrollment__PrivateKeyPem"] =
    map["SECURITY_ENROLLMENT_PRIVATE_KEY_PEM"].Replace("\\n", "\n", StringComparison.Ordinal);
psi.Environment["Security__Enrollment__PublicKeyPem"] =
    map["SECURITY_ENROLLMENT_PUBLIC_KEY_PEM"].Replace("\\n", "\n", StringComparison.Ordinal);
psi.Environment["Pilot__TerminalId"] =
    map.GetValueOrDefault("PILOT_TERMINAL_ID", "00000000-0000-0000-0000-000000000010");
psi.Environment["Pilot__OwnerPassword"] =
    map.GetValueOrDefault("PILOT_OWNER_PASSWORD", "ammar123");
psi.Environment["Pilot__AdminPassword"] =
    map.GetValueOrDefault("PILOT_ADMIN_PASSWORD", "admin123");
psi.Environment["Pilot__OwnerPin"] =
    map.GetValueOrDefault("PILOT_OWNER_PIN", "9753");
psi.Environment["Pilot__ManagerPin"] =
    map.GetValueOrDefault("PILOT_MANAGER_PIN", "8642");
psi.Environment["Pilot__CashierPin"] =
    map.GetValueOrDefault("PILOT_CASHIER_PIN", "2468");
psi.Environment["Pilot__AbcCashierPin"] =
    map.GetValueOrDefault("PILOT_ABC_CASHIER_PIN", "1357");

var proc = Process.Start(psi)!;
File.WriteAllText(pidFile, proc.Id.ToString());
Console.WriteLine($"Pilot API started pid={proc.Id} url=http://localhost:8080");

static void StopExistingPilot(string pidFile)
{
    if (!File.Exists(pidFile))
    {
        return;
    }

    string text = File.ReadAllText(pidFile).Trim();
    if (int.TryParse(text, out int pid))
    {
        try
        {
            using Process existing = Process.GetProcessById(pid);
            Console.WriteLine($"Stopping previous pilot API pid={pid}...");
            existing.Kill(entireProcessTree: true);
            existing.WaitForExit(10_000);
        }
        catch (ArgumentException)
        {
            // Process already gone.
        }
        catch (InvalidOperationException)
        {
            // Process already gone.
        }
    }

    File.Delete(pidFile);
}

static void TryStopListenerOnPort(int port)
{
    try
    {
        IPGlobalProperties props = IPGlobalProperties.GetIPGlobalProperties();
        bool inUse = props.GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);
        if (inUse)
        {
            Console.WriteLine(
                $"Warning: port {port} is already in use. Stop Docker api or the other process before local pilot start.");
        }
    }
    catch (NetworkInformationException)
    {
        // Best-effort only.
    }
}
'@ | Set-Content (Join-Path $genDir "Program.cs") -Encoding utf8

dotnet run --project (Join-Path $genDir "Start.csproj") -- $root

