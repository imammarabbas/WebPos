using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Security;
using WebPos.Filters;

namespace WebPos.IntegrationTests.Security;

public sealed class JwtAuthorizationTests
{
    private static readonly Guid TenantId = TenantDefaults.MasterTenantId;

    [Fact]
    public async Task AuthenticateAsync_ShouldIssueTokenWithSubTidAndRoleClaims()
    {
        (JwtService service, JwtOptions options, SymmetricSecurityKey key, User user) =
            await CreateServiceWithUserAsync("token-admin", "Str0ng!Pass");

        AuthResponse? response = await service.AuthenticateAsync(new LoginRequest
        {
            Username = "token-admin",
            Password = "Str0ng!Pass"
        });

        response.Should().NotBeNull();
        response!.TokenType.Should().Be("Bearer");
        response.Role.Should().Be("Admin");

        // Validate exactly like Program.cs: same key, issuer, audience, zero clock skew.
        TokenValidationParameters validation = new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };
        ClaimsPrincipal principal = handler.ValidateToken(
            response.AccessToken, validation, out _);

        principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value
            .Should().Be(user.Id.ToString());
        principal.FindFirst(JwtService.TenantClaimType)!.Value
            .Should().Be(TenantId.ToString());
        principal.FindFirst(JwtService.RoleClaimType)!.Value.Should().Be("Admin");
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnNullForWrongPassword()
    {
        (JwtService service, _, _, _) =
            await CreateServiceWithUserAsync("token-user", "correct-pass");

        AuthResponse? response = await service.AuthenticateAsync(new LoginRequest
        {
            Username = "token-user",
            Password = "wrong-pass"
        });

        response.Should().BeNull();
    }

    [Fact]
    public void TenantAuthorize_ShouldReturn401_WhenUnauthenticated()
    {
        AuthorizationFilterContext context = CreateFilterContext(
            new ClaimsPrincipal(new ClaimsIdentity()),
            routeTenantId: null,
            serviceTenantId: TenantId);

        new TenantAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public void TenantAuthorize_ShouldReturn403_WhenRouteTenantMismatches()
    {
        ClaimsPrincipal user = CreateJwtPrincipal(TenantId);
        AuthorizationFilterContext context = CreateFilterContext(
            user,
            routeTenantId: Guid.NewGuid(),
            serviceTenantId: TenantId);

        new TenantAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void TenantAuthorize_ShouldReturn403_WhenTokenTenantDiffersFromResolvedTenant()
    {
        ClaimsPrincipal user = CreateJwtPrincipal(Guid.NewGuid());
        AuthorizationFilterContext context = CreateFilterContext(
            user,
            routeTenantId: null,
            serviceTenantId: TenantId);

        new TenantAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void TenantAuthorize_ShouldPass_WhenTidMatchesRouteAndResolvedTenant()
    {
        ClaimsPrincipal user = CreateJwtPrincipal(TenantId);
        AuthorizationFilterContext context = CreateFilterContext(
            user,
            routeTenantId: TenantId,
            serviceTenantId: TenantId);

        new TenantAuthorizeAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    private static async Task<(JwtService Service, JwtOptions Options, SymmetricSecurityKey Key, User User)>
        CreateServiceWithUserAsync(string username, string password)
    {
        DbContextOptions<WebPosDbContext> dbOptions =
            new DbContextOptionsBuilder<WebPosDbContext>()
                .UseInMemoryDatabase($"WebPos_Jwt_{Guid.NewGuid():N}")
                .Options;

        var tenantService = new TestTenantService(TenantId);
        var context = new WebPosDbContext(dbOptions, tenantService);

        context.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Master Tenant",
            Slug = $"master-{TenantId:N}",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        Guid roleId = Guid.NewGuid();
        context.Roles.Add(new Role
        {
            Id = roleId,
            TenantId = TenantId,
            RoleName = "Admin",
            CreatedAt = DateTimeOffset.UtcNow
        });

        User user = new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Username = username,
            PasswordHash = CryptoHelper.HashPassword(password),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        JwtOptions options = new()
        {
            Key = "unit-test-signing-key-material-0123456789",
            Issuer = "WebPos",
            Audience = "WebPos.Api",
            AccessTokenLifetimeMinutes = 30
        };
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(options.Key));

        IDbContextFactory<WebPosDbContext> dbFactory =
            new TestDbContextFactory(dbOptions, tenantService);
        JwtService service = new(
            dbFactory,
            tenantService,
            new JwtSigningKey(key),
            Microsoft.Extensions.Options.Options.Create(options));

        return (service, options, key, user);
    }

    private static ClaimsPrincipal CreateJwtPrincipal(Guid tenantId) =>
        new(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtService.TenantClaimType, tenantId.ToString()),
            new Claim(JwtService.RoleClaimType, "Admin")
        ],
        authenticationType: "AuthenticationTypes.Federation"));

    private static AuthorizationFilterContext CreateFilterContext(
        ClaimsPrincipal user,
        Guid? routeTenantId,
        Guid serviceTenantId)
    {
        ServiceCollection services = new();
        services.AddSingleton<ITenantService>(new TestTenantService(serviceTenantId));

        DefaultHttpContext httpContext = new()
        {
            User = user,
            RequestServices = services.BuildServiceProvider()
        };

        RouteData routeData = new();
        if (routeTenantId is Guid tenantId)
        {
            routeData.Values["tenantId"] = tenantId.ToString();
        }

        return new AuthorizationFilterContext(
            new ActionContext(httpContext, routeData, new ActionDescriptor()),
            []);
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<WebPosDbContext> options,
        ITenantService tenant) : IDbContextFactory<WebPosDbContext>
    {
        public WebPosDbContext CreateDbContext() => new(options, tenant);
    }

    private sealed class TestTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }
}
