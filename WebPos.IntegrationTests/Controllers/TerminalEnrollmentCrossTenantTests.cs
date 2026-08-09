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

/// <summary>
/// After restore, an orphan admin on the terminal's store tenant can have a stale password
/// while Master portal logs into the master-tenant admin. Enroll must pick the matching password.
/// </summary>
public sealed class TerminalEnrollmentCrossTenantTests
{
    private const string AdminPassword = "admin123";
    private static readonly Guid PilotTerminalId =
        Guid.Parse("00000000-0000-0000-0000-000000000010");

    [Fact]
    public async Task Enroll_Should_UseMasterAdminPassword_WhenTerminalTenantHasOrphanAdmin()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await SeedCrossTenantScenarioAsync(factory);

        using HttpResponseMessage enrollResponse = await ApiTestClient.SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/terminal-enrollment",
            new EnrollTerminalRequest
            {
                TerminalId = PilotTerminalId,
                AdminUsername = "admin",
                AdminPassword = AdminPassword
            },
            bearerToken: null);

        enrollResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        EnrollmentCertificateDto? cert =
            await enrollResponse.Content.ReadFromJsonAsync<EnrollmentCertificateDto>();
        cert.Should().NotBeNull();
        cert!.Token.Should().NotBeNullOrWhiteSpace();
        cert.TenantId.Should().Be(TenantDefaults.MasterTenantId);
        cert.TerminalId.Should().Be(PilotTerminalId);

        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        Terminal terminal = await db.Terminals
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Id == PilotTerminalId);
        terminal.TenantId.Should().Be(TenantDefaults.MasterTenantId);
    }

    private static async Task SeedCrossTenantScenarioAsync(SalesApiFactory factory)
    {
        await factory.ResetDatabaseAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid masterTenantId = TenantDefaults.MasterTenantId;
        Guid storeTenantId = Guid.Parse("00000000-0000-0000-0000-000000000099");
        Guid masterManagerRoleId = Guid.NewGuid();
        Guid storeManagerRoleId = Guid.NewGuid();

        db.Tenants.AddRange(
            new Tenant
            {
                Id = masterTenantId,
                Name = "Copenhagen Mart",
                Slug = "copenhagen-mart",
                IsActive = true,
                CreatedAt = now
            },
            new Tenant
            {
                Id = storeTenantId,
                Name = "Orphan Store",
                Slug = "orphan-store",
                IsActive = true,
                CreatedAt = now
            });

        db.Roles.AddRange(
            new Role
            {
                Id = masterManagerRoleId,
                TenantId = masterTenantId,
                RoleName = "Manager",
                CreatedAt = now
            },
            new Role
            {
                Id = storeManagerRoleId,
                TenantId = storeTenantId,
                RoleName = "Manager",
                CreatedAt = now
            });

        // Master portal admin — correct password.
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            TenantId = masterTenantId,
            Username = "admin",
            PasswordHash = CryptoHelper.HashPassword(AdminPassword),
            PinHash = string.Empty,
            RoleId = masterManagerRoleId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        // Orphan on the terminal's store — wrong password (would steal enroll if preferred blindly).
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            TenantId = storeTenantId,
            Username = "admin",
            PasswordHash = CryptoHelper.HashPassword("wrong-orphan-password"),
            PinHash = string.Empty,
            RoleId = storeManagerRoleId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.Terminals.Add(new Terminal
        {
            Id = PilotTerminalId,
            TenantId = storeTenantId,
            TerminalName = "Cross-tenant POS",
            MacAddress = "CROSS-TENANT-001",
            IsActive = true,
            LastSyncTime = now
        });

        await db.SaveChangesAsync();
    }
}
