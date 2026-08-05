using System.Net;
using System.Net.Http.Json;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Security;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Login_ShouldReturnCashier_WhenPinIsValid()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        const string pin = "2468";
        User cashier = await SeedCashierAsync(factory, pin);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/auth/login",
            TestEnrollmentAuth.DefaultTenantId,
            Guid.NewGuid(),
            new LoginRequest { Pin = pin },
            includeEnrollmentAuth: false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CashierDto? result = await response.Content.ReadFromJsonAsync<CashierDto>();
        result.Should().NotBeNull();
        result!.CashierId.Should().Be(cashier.Id);
        result.Name.Should().Be(cashier.Username);
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenPinIsInvalid()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/auth/login",
            TestEnrollmentAuth.DefaultTenantId,
            Guid.NewGuid(),
            new LoginRequest { Pin = "9999" },
            includeEnrollmentAuth: false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyManagerPin_ShouldAcceptManagerAndRejectCashier()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        const string managerPin = "8642";
        const string cashierPin = "1357";
        await SeedUserWithRoleAsync(factory, "Manager", managerPin);
        await SeedUserWithRoleAsync(factory, "Cashier", cashierPin);

        using HttpResponseMessage managerLogin = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/auth/login",
            TestEnrollmentAuth.DefaultTenantId,
            Guid.NewGuid(),
            new LoginRequest { Pin = managerPin },
            includeEnrollmentAuth: false);
        string managerLoginBody = await managerLogin.Content.ReadAsStringAsync();
        managerLogin.StatusCode.Should().Be(HttpStatusCode.OK, managerLoginBody);

        using HttpResponseMessage managerOk = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/auth/verify-manager-pin",
            TestEnrollmentAuth.DefaultTenantId,
            Guid.NewGuid(),
            new LoginRequest { Pin = managerPin },
            includeEnrollmentAuth: false);
        string managerBody = await managerOk.Content.ReadAsStringAsync();
        managerOk.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "verify-manager-pin response: {0}",
            managerBody);

        using HttpResponseMessage cashierDenied = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/auth/verify-manager-pin",
            TestEnrollmentAuth.DefaultTenantId,
            Guid.NewGuid(),
            new LoginRequest { Pin = cashierPin },
            includeEnrollmentAuth: false);
        cashierDenied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<User> SeedUserWithRoleAsync(
        SalesApiFactory factory,
        string roleName,
        string pin)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        IPinHasher pinHasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();

        Guid tenantId = TestEnrollmentAuth.DefaultTenantId;
        Role? existingRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.RoleName == roleName && candidate.TenantId == tenantId);
        Role role = existingRole ?? new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleName = roleName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (existingRole is null)
        {
            context.Roles.Add(role);
            await context.SaveChangesAsync();
        }

        User user = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"{roleName.ToLowerInvariant()}-{Guid.NewGuid():N}",
            PasswordHash = CryptoHelper.HashPassword("legacy-password"),
            PinHash = pinHasher.HashPin(pin),
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<User> SeedCashierAsync(SalesApiFactory factory, string pin)
    {
        return await SeedUserWithRoleAsync(factory, "Cashier", pin);
    }
}
