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

    private static async Task<User> SeedCashierAsync(SalesApiFactory factory, string pin)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        IPinHasher pinHasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();

        Guid tenantId = TestEnrollmentAuth.DefaultTenantId;
        Role role = await context.Roles.FirstOrDefaultAsync(
            candidate => candidate.RoleName == "Cashier" && candidate.TenantId == tenantId)
            ?? new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RoleName = "Cashier",
                CreatedAt = DateTimeOffset.UtcNow
            };

        if (!await context.Roles.AnyAsync(r => r.Id == role.Id))
        {
            context.Roles.Add(role);
            await context.SaveChangesAsync();
        }

        User cashier = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"pin-cashier-{Guid.NewGuid():N}",
            PasswordHash = CryptoHelper.HashPassword("legacy-password"),
            PinHash = pinHasher.HashPin(pin),
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Users.Add(cashier);
        await context.SaveChangesAsync();
        return cashier;
    }
}
