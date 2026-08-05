using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Controllers;

public sealed class StoreControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetStatus_ShouldReflectOpenAndClosedShifts()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        (Guid cashierId, Guid terminalId) = await SeedCashierAndTerminalAsync(factory);

        using HttpResponseMessage closedResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/store/status",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId);

        closedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        StoreStatusDto? closed = await ApiTestClient.ReadJsonAsync<StoreStatusDto>(
            closedResponse,
            JsonOptions);
        closed.Should().NotBeNull();
        closed!.TillOpen.Should().BeFalse();
        closed.OpenShiftCount.Should().Be(0);

        using HttpResponseMessage startResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/shift/start",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId,
            new StartShiftRequest
            {
                CashierId = cashierId,
                TerminalId = terminalId,
                OpeningCashPaisa = 10_000
            });

        startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftDto? shift = await startResponse.Content.ReadFromJsonAsync<ShiftDto>();
        shift.Should().NotBeNull();

        using HttpResponseMessage openResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/store/status",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId);

        openResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        StoreStatusDto? open = await ApiTestClient.ReadJsonAsync<StoreStatusDto>(
            openResponse,
            JsonOptions);
        open.Should().NotBeNull();
        open!.TillOpen.Should().BeTrue();
        open.OpenShiftCount.Should().BeGreaterThan(0);

        using HttpResponseMessage forceClose = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            $"/api/shift/{shift!.ShiftId:D}/force-close",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId);

        // Force-close requires Owner/Manager JWT; enrollment token may be forbidden.
        // Close via blind count instead when force-close is role-gated.
        if (forceClose.StatusCode == HttpStatusCode.Forbidden
            || forceClose.StatusCode == HttpStatusCode.Unauthorized)
        {
            using HttpResponseMessage closeResponse = await ApiTestClient.SendAsync(
                client,
                factory,
                HttpMethod.Post,
                "/api/shift/close",
                TestEnrollmentAuth.DefaultTenantId,
                terminalId,
                new CloseShiftRequest
                {
                    ShiftId = shift.ShiftId,
                    CashierId = cashierId,
                    TerminalId = terminalId,
                    ActualCashPaisa = 10_000
                });
            closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        else
        {
            forceClose.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using HttpResponseMessage afterClose = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/store/status",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId);

        afterClose.StatusCode.Should().Be(HttpStatusCode.OK);
        StoreStatusDto? closedAgain = await ApiTestClient.ReadJsonAsync<StoreStatusDto>(
            afterClose,
            JsonOptions);
        closedAgain.Should().NotBeNull();
        closedAgain!.TillOpen.Should().BeFalse();
    }

    private static async Task<(Guid CashierId, Guid TerminalId)> SeedCashierAndTerminalAsync(
        SalesApiFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        Guid tenantId = TestEnrollmentAuth.DefaultTenantId;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Role? existingRole = await context.Roles.FirstOrDefaultAsync(
            role => role.RoleName == "Cashier" && role.TenantId == tenantId);
        Guid roleId = existingRole?.Id ?? Guid.NewGuid();
        if (existingRole is null)
        {
            context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = tenantId,
                RoleName = "Cashier",
                CreatedAt = now
            });
        }

        User cashier = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"store-cashier-{Guid.NewGuid():N}",
            PasswordHash = "x",
            RoleId = roleId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        Terminal terminal = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TerminalName = $"Store Terminal {Guid.NewGuid():N}",
            MacAddress = Guid.NewGuid().ToString("N"),
            IsActive = true,
            LastSyncTime = now
        };

        context.Users.Add(cashier);
        context.Terminals.Add(terminal);
        await context.SaveChangesAsync();
        return (cashier.Id, terminal.Id);
    }
}
