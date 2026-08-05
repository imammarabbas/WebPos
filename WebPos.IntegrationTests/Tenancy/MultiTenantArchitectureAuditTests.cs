using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using WebPos.Client.Sdk.Security;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Services;

namespace WebPos.IntegrationTests.Tenancy;

public sealed class MultiTenantArchitectureAuditTests
{
    private const string ConnectionString =
        "Server=localhost;Port=5432;Database=WebPos_Test;User Id=postgres;Password=sa;";

    [Fact]
    public async Task RandomTenant_ShouldNotReadProductsOrSalesInvoices()
    {
        await using AuditDatabase audit = await AuditDatabase.CreateAsync();
        Guid productId = Guid.NewGuid();
        string invoiceNo = $"QA-{Guid.NewGuid():N}";
        await using (WebPosDbContext masterContext =
                     await audit.CreateContextAsync(
                         new TestTenantService(TenantDefaults.MasterTenantId)))
        {
            AddMasterTenantSalesGraph(masterContext, productId, invoiceNo);
            await masterContext.SaveChangesAsync();
        }

        await using WebPosDbContext randomTenantContext =
            await audit.CreateContextAsync(new TestTenantService(Guid.NewGuid()));

        int visibleProducts = await randomTenantContext.Products
            .CountAsync(product => product.Id == productId);
        int visibleInvoices = await randomTenantContext.SalesInvoices
            .CountAsync(invoice => invoice.InvoiceNo == invoiceNo);

        visibleProducts.Should().Be(0);
        visibleInvoices.Should().Be(0);
        (await randomTenantContext.Products.IgnoreQueryFilters()
            .CountAsync(product => product.Id == productId)).Should().Be(1);
        (await randomTenantContext.SalesInvoices.IgnoreQueryFilters()
            .CountAsync(invoice => invoice.InvoiceNo == invoiceNo)).Should().Be(1);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistStampedTenantId()
    {
        await using AuditDatabase audit = await AuditDatabase.CreateAsync();
        Guid productId = Guid.NewGuid();
        await using (WebPosDbContext context =
                     await audit.CreateContextAsync(
                         new TestTenantService(TenantDefaults.MasterTenantId)))
        {
            context.Products.Add(CreateProduct(productId));
            await context.SaveChangesAsync();
        }

        await using NpgsqlCommand command = new(
            """
            SELECT tenant_id
            FROM products
            WHERE id = @productId;
            """,
            audit.Connection,
            audit.Transaction);
        command.Parameters.AddWithValue("productId", productId);

        object? persistedValue = await command.ExecuteScalarAsync();

        persistedValue.Should().Be(TenantDefaults.MasterTenantId);
        persistedValue.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task AuthenticatedTenantIdentity_ShouldControlSubsequentReadsAndWrites()
    {
        await using AuditDatabase audit = await AuditDatabase.CreateAsync();
        Guid configuredTenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new Claim(
                        HttpContextTenantService.TenantClaimType,
                        configuredTenantId.ToString()),
                    new Claim(
                        EnrollmentCertificateValidator.CertificateTypeClaim,
                        EnrollmentCertificateValidator.EnrollmentCertificateType)
                ],
                authenticationType:
                    EnrollmentCertificateValidator.AuthenticationType))
        };
        var tenantService = new HttpContextTenantService(
            new HttpContextAccessor { HttpContext = httpContext },
            new CircuitTenantContext());
        Guid firstProductId = Guid.NewGuid();
        Guid secondProductId = Guid.NewGuid();

        await using (WebPosDbContext writeContext =
                     await audit.CreateContextAsync(tenantService))
        {
            writeContext.Tenants.Add(new Tenant
            {
                Id = configuredTenantId,
                Name = "QA Tenant",
                Slug = $"qa-{configuredTenantId:N}",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            writeContext.Products.Add(CreateProduct(firstProductId));
            await writeContext.SaveChangesAsync();
        }

        await using WebPosDbContext subsequentContext =
            await audit.CreateContextAsync(tenantService);
        (await subsequentContext.Products
            .CountAsync(product => product.Id == firstProductId)).Should().Be(1);

        Product secondProduct = CreateProduct(secondProductId);
        subsequentContext.Products.Add(secondProduct);
        await subsequentContext.SaveChangesAsync();

        secondProduct.TenantId.Should().Be(configuredTenantId);
        (await subsequentContext.Products
            .CountAsync(product =>
                product.Id == firstProductId || product.Id == secondProductId))
            .Should().Be(2);
        (await subsequentContext.Products
            .CountAsync(product =>
                product.TenantId != configuredTenantId))
            .Should().Be(0);
    }

    private static Product CreateProduct(Guid id, Guid tenantId = default) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = "QA Tenant Product",
            Sku = $"QA-SKU-{id:N}",
            Barcode = $"QA-BAR-{id:N}",
            Brand = "QA",
            BaseUnit = "PCS",
            ConversionMultiplier = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static void AddMasterTenantSalesGraph(
        WebPosDbContext context,
        Guid productId,
        string invoiceNo)
    {
        Guid tenantId = TenantDefaults.MasterTenantId;
        Guid roleId = Guid.NewGuid();
        Guid cashierId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        Guid shiftId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (!context.Tenants.Any(t => t.Id == tenantId))
        {
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Master Tenant",
                Slug = "master",
                IsActive = true,
                CreatedAt = now
            });
        }

        context.AddRange(
            new Role
            {
                Id = roleId,
                TenantId = tenantId,
                RoleName = $"QA-{roleId:N}",
                CreatedAt = now
            },
            new User
            {
                Id = cashierId,
                TenantId = tenantId,
                Username = $"qa-{cashierId:N}",
                PasswordHash = "QA",
                RoleId = roleId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Terminal
            {
                Id = terminalId,
                TenantId = tenantId,
                TerminalName = $"QA-{terminalId:N}",
                MacAddress = $"QA-{terminalId:N}",
                IsActive = true,
                LastSyncTime = now
            },
            new CashierShift
            {
                Id = shiftId,
                TenantId = tenantId,
                TerminalId = terminalId,
                CashierId = cashierId,
                OpenedAt = now,
                ClosedAt = now,
                Status = "CLOSED"
            },
            new SalesInvoice
            {
                TenantId = tenantId,
                InvoiceNo = invoiceNo,
                ShiftId = shiftId,
                TerminalId = terminalId,
                CashierId = cashierId,
                TotalAmountPaisa = 100,
                ReceiptNumber = $"QA-{Guid.NewGuid():N}",
                PaymentMethod = "CASH",
                CreatedAt = now
            },
            CreateProduct(productId, tenantId));
    }

    private sealed class TestTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }

    private sealed class AuditDatabase : IAsyncDisposable
    {
        private readonly DbContextOptions<WebPosDbContext> _options;

        private AuditDatabase(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
            _options = new DbContextOptionsBuilder<WebPosDbContext>()
                .UseNpgsql(connection)
                .Options;
        }

        public NpgsqlConnection Connection { get; }

        public NpgsqlTransaction Transaction { get; }

        public static async Task<AuditDatabase> CreateAsync()
        {
            NpgsqlConnection connection = new(ConnectionString);
            await connection.OpenAsync();
            NpgsqlTransaction transaction =
                await connection.BeginTransactionAsync();
            return new AuditDatabase(connection, transaction);
        }

        public async Task<WebPosDbContext> CreateContextAsync(
            ITenantService tenantService)
        {
            var context = new WebPosDbContext(_options, tenantService);
            await context.Database.UseTransactionAsync(Transaction);
            return context;
        }

        public async ValueTask DisposeAsync()
        {
            await Transaction.RollbackAsync();
            await Transaction.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
