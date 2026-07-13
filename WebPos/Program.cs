using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using WebPos.Components;
using WebPos.Configuration;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Models;
using WebPos.Core.Security;
using WebPos.Core.Services;
using WebPos.Filters;

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

builder.Services.AddHttpContextAccessor();

// --- Data (Scoped per request/circuit) ---
builder.Services.AddDbContext<WebPosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Authentication & authorization ---
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, PosAuthStateProvider>();
builder.Services.AddScoped<AuthService>();

// --- Domain services (Scoped: transactional DbContext state) ---
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<PartyLedgerService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<ISalesReturnService, SalesReturnService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IReportingService, ReportingService>();

// --- Process-wide caches (Singleton: shared in-memory catalogue) ---
builder.Services.AddSingleton<ProductCacheService>();

var app = builder.Build();

ProductCacheService productCache = app.Services.GetRequiredService<ProductCacheService>();
await productCache.LoadCatalogAsync();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();

        if (!await context.Roles.AnyAsync())
        {
            context.Roles.AddRange(
                new Role { Id = Guid.NewGuid(), RoleName = "Admin", CreatedAt = DateTimeOffset.UtcNow },
                new Role { Id = Guid.NewGuid(), RoleName = "Cashier", CreatedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync();
        }

        Role? adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
        Role? cashierRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Cashier");

        if (adminRole is null)
        {
            adminRole = new Role { Id = Guid.NewGuid(), RoleName = "Admin", CreatedAt = DateTimeOffset.UtcNow };
            context.Roles.Add(adminRole);
            await context.SaveChangesAsync();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
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
                Username = "cashier",
                PasswordHash = CryptoHelper.HashPassword("cashier123"),
                RoleId = cashierRole.Id,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
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