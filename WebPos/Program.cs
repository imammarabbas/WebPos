using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using WebPos.Core.Abstractions;
using WebPos.Components;
using WebPos.Configuration;
using WebPos;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Security;
using WebPos.Core.Services;
using WebPos.Filters;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Security;

var builder = WebApplication.CreateBuilder(args);

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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Authentication & authorization ---
builder.Services.AddCascadingAuthenticationState();

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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
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

ProductCacheService productCache = app.Services.GetRequiredService<ProductCacheService>();
await productCache.LoadCatalogAsync();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseMiddleware<WebPos.Middleware.EnrollmentCertificateMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseAntiforgery();
}

app.MapStaticAssets();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("StartupSeeding");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid masterTenantId = TenantDefaults.MasterTenantId;

        if (!await context.Tenants.AnyAsync(t => t.Id == masterTenantId))
        {
            context.Tenants.Add(new Tenant
            {
                Id = masterTenantId,
                Name = "Master Tenant",
                Slug = "master",
                IsActive = true,
                CreatedAt = now
            });
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded master tenant {TenantId}", masterTenantId);
        }

        if (!await context.Roles.AnyAsync())
        {
            context.Roles.AddRange(
                new Role
                {
                    Id = Guid.NewGuid(),
                    TenantId = masterTenantId,
                    RoleName = "Admin",
                    CreatedAt = now
                },
                new Role
                {
                    Id = Guid.NewGuid(),
                    TenantId = masterTenantId,
                    RoleName = "Cashier",
                    CreatedAt = now
                });
            await context.SaveChangesAsync();
        }

        Role? adminRole = await context.Roles.FirstOrDefaultAsync(
            r => r.RoleName == "Admin" && r.TenantId == masterTenantId);
        Role? cashierRole = await context.Roles.FirstOrDefaultAsync(
            r => r.RoleName == "Cashier" && r.TenantId == masterTenantId);

        if (adminRole is null)
        {
            adminRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = masterTenantId,
                RoleName = "Admin",
                CreatedAt = now
            };
            context.Roles.Add(adminRole);
            await context.SaveChangesAsync();
        }

        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = masterTenantId,
                Username = "admin",
                PasswordHash = CryptoHelper.HashPassword("admin123"),
                RoleId = adminRole.Id,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (cashierRole is not null && !await context.Users.AnyAsync(u => u.Username == "cashier"))
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = masterTenantId,
                Username = "cashier",
                PasswordHash = CryptoHelper.HashPassword("cashier123"),
                RoleId = cashierRole.Id,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Pilot terminal for enrollment (override id via env Pilot__TerminalId if needed).
        string? pilotTerminalIdRaw = app.Configuration["Pilot:TerminalId"];
        Guid pilotTerminalId = Guid.TryParse(pilotTerminalIdRaw, out Guid parsed)
            ? parsed
            : Guid.Parse("00000000-0000-0000-0000-000000000010");

        if (!await context.Terminals.AnyAsync(t => t.Id == pilotTerminalId))
        {
            context.Terminals.Add(new Terminal
            {
                Id = pilotTerminalId,
                TenantId = masterTenantId,
                TerminalName = "Pilot POS Terminal",
                MacAddress = "PILOT-TERMINAL-001",
                IsActive = true,
                LastSyncTime = now
            });
            logger.LogInformation(
                "Seeded pilot terminal {TerminalId} for enrollment",
                pilotTerminalId);
        }

        await context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Identity seeding failed; application will continue startup.");
    }
}

app.Run();

public partial class Program;