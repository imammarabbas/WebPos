using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using WebPos.Core.Abstractions;
using WebPos.Components;
using WebPos.Configuration;
using WebPos.Core;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Security;
using WebPos.Core.Seeding;
using WebPos.Core.Services;
using WebPos.Filters;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Security;

var builder = WebApplication.CreateBuilder(args);

// Local override only for Development — must not override Docker Production (Server=postgres).
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Development.Local.json",
        optional: true,
        reloadOnChange: true);
}

// Fill missing Security:* values for local / docker-compose zero-config boots.
ApplyInsecureDevSecurityDefaults(builder);

// --- Blazor & HTTP ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<ApiVersionOptions>(builder.Configuration.GetSection(ApiVersionOptions.SectionName));
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiVersionFilter>();
});

builder.Services.AddExceptionHandler<WebPos.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<SyncSchemaVersionFilter>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, HttpContextTenantService>();

// --- Data (Scoped per request/circuit) ---
builder.Services.AddDbContext<WebPosDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("WebPos")));

// Scoped factory so Blazor layout + page can run concurrent queries without sharing one DbContext.
builder.Services.AddDbContextFactory<WebPosDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("WebPos")),
    lifetime: ServiceLifetime.Scoped);

// --- Authentication & authorization ---
builder.Services.AddCascadingAuthenticationState();

// Persist DP keys so cookie/antiforgery survive container restarts (Docker volume).
string dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "dp-keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("WebPos");

// JWT signing key: from Security:Jwt:Key (env/Key Vault). Ephemeral in dev so
// no secret is ever generated into local config; tokens die with the process.
JwtOptions jwtOptions =
    builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
SymmetricSecurityKey jwtSigningKey = string.IsNullOrWhiteSpace(jwtOptions.Key)
    ? new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32))
    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton(new JwtSigningKey(jwtSigningKey));

builder.Services.AddSingleton<LoginTicketStore>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "WebPos.Auth";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep raw sub/tid/role claim names
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSigningKey,
            ValidateLifetime = true,
            // Hard expiry: no grace window for lingering POS sessions.
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = JwtService.RoleClaimType,
            NameClaimType = "unique_name"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<ISecurityService, JwtService>();
builder.Services.AddScoped<AuthenticationStateProvider, PosAuthStateProvider>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<IPinHasher, HmacPinHasher>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddSingleton<IKeyProvider, ConfigurationKeyProvider>();
builder.Services.AddSingleton<
    IEnrollmentCertificateValidator,
    EnrollmentCertificateValidator>();
builder.Services.AddScoped<EnrollmentService>();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("pin-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("terminal-enrollment", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// --- Domain services (Scoped: keeps ITenantService aligned for the request) ---
builder.Services.AddWebPosDomainServices();

// --- Outbound WebPos API SDK (IHttpClientFactory) ---
builder.Services.AddWebPosSdk(builder.Configuration);

// --- Health checks ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WebPosDbContext>("database");

// --- Process-wide caches (Singleton: shared in-memory catalogue) ---
builder.Services.AddSingleton<ProductCacheService>();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
    var pinHasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupSeeding");

    try
    {
        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
            app.Logger.LogInformation(
                "EF Core migrations applied (schema create/update is automatic — no backup.sql required).");
        }

        await PilotDataSeeder.SeedAsync(context, pinHasher, app.Configuration, logger);
        app.Logger.LogInformation(
            "Startup seed complete (roles, catalog, terminals, cash accounts).");
    }
    catch (Exception ex)
    {
        // Fail fast: an unmigrated or unreachable DB should not silently continue.
        app.Logger.LogCritical(
            ex,
            "Database migrate/seed failed. Fix the connection string / PostgreSQL service and restart.");
        throw;
    }
}

ProductCacheService productCache = app.Services.GetRequiredService<ProductCacheService>();
await productCache.LoadCatalogAsync();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (!app.Environment.IsEnvironment("Testing")
    && !app.Environment.IsEnvironment("Pilot"))
{
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
}

if (!app.Environment.IsDevelopment()
    && !app.Environment.IsEnvironment("Testing")
    && !app.Environment.IsEnvironment("Pilot"))
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment()
    && !app.Environment.IsEnvironment("Testing")
    && !app.Environment.IsEnvironment("Pilot")
    && !ShouldDisableHttpsRedirection(app))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseMiddleware<WebPos.Middleware.EnrollmentCertificateMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

// Blazor endpoints require antiforgery. Skip only in Testing (integration TestServer).
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseAntiforgery();
}

app.MapStaticAssets();
app.MapAccountEndpoints();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>
/// When Security keys are blank, inject ephemeral/local-only defaults so a new machine
/// can <c>docker compose up</c> / <c>dotnet run</c> without Generate-PilotEnv.
/// Never used for Testing. Enable explicitly with Security:AllowInsecureDevDefaults=true
/// (set by docker-compose) or automatically in Development.
/// </summary>
static void ApplyInsecureDevSecurityDefaults(WebApplicationBuilder builder)
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

    if (string.IsNullOrWhiteSpace(config["Security:PinHashKey"]))
    {
        // 32 stable bytes derived from a fixed local salt (HMAC keys must be ≥32 bytes).
        byte[] pinKey = SHA256.HashData("WebPos.Dev.PinHashKey.v1"u8.ToArray());
        config["Security:PinHashKey"] = Convert.ToBase64String(pinKey);
        injected = true;
    }

    if (string.IsNullOrWhiteSpace(config["Security:Jwt:Key"]))
    {
        config["Security:Jwt:Key"] = "dev_jwt_secret_key_must_be_32_chars_min";
        injected = true;
    }

    bool missingPrivate = string.IsNullOrWhiteSpace(config["Security:Enrollment:PrivateKeyPem"]);
    bool missingPublic = string.IsNullOrWhiteSpace(config["Security:Enrollment:PublicKeyPem"]);
    if (missingPrivate || missingPublic)
    {
        using RSA rsa = RSA.Create(2048);
        string privatePem = rsa.ExportPkcs8PrivateKeyPem().Replace("\r\n", "\n", StringComparison.Ordinal);
        string publicPem = rsa.ExportSubjectPublicKeyInfoPem().Replace("\r\n", "\n", StringComparison.Ordinal);
        config["Security:Enrollment:PrivateKeyPem"] = privatePem;
        config["Security:Enrollment:PublicKeyPem"] = publicPem;
        injected = true;
    }

    if (injected)
    {
        Console.WriteLine(
            "WARNING: Security:* defaults were auto-generated for local/docker use. " +
            "Do not use these secrets in production. Prefer scripts/Generate-PilotEnv.ps1 for stable pilot keys.");
    }
}

static bool ShouldDisableHttpsRedirection(WebApplication app)
{
    if (string.Equals(
            app.Configuration["DisableHttpsRedirection"],
            "true",
            StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    string? urls = app.Configuration["ASPNETCORE_URLS"]
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (string.IsNullOrWhiteSpace(urls))
    {
        return false;
    }

    // Docker / local HTTP-only hosts (e.g. http://+:8080) must not force HTTPS.
    return urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .All(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
}

public partial class Program;