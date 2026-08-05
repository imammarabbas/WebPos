using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Services;

namespace WebPos.IntegrationTests.Services;

public sealed class ReportingServiceTests
{
    [Fact]
    public async Task Queries_ShouldIsolateByTenant()
    {
        await using ReportingHarness harness = await ReportingHarness.CreateAsync();
        Guid otherTenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await harness.SeedForeignTenantAsync(otherTenantId);

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(1);

        IReadOnlyList<ExpenseCategorySummary> expenses =
            await harness.ReportingService.GetExpenseSummaryByCategoryAsync(from, to);
        expenses.Should().ContainSingle(e => e.ExpenseCategory == ExpenseCategories.Maintenance);
        expenses.Single().TotalAmountPaisa.Should().Be(100_00);

        long maintenance =
            await harness.ReportingService.GetMaintenanceSpendAsync(from, to);
        // Ledger SSOT (150_00), not ShiftExpenses (100_00) and not Math.Max of both.
        maintenance.Should().Be(150_00);

        IReadOnlyList<ProductProfitSummary> profit =
            await harness.ReportingService.GetGrossProfitByProductAsync(from, to);
        profit.Should().ContainSingle();
        profit[0].RevenuePaisa.Should().Be(200_00);
        profit[0].CostPaisa.Should().Be(100_00);
        profit[0].GrossProfitPaisa.Should().Be(100_00);
        profit[0].MarginPercent.Should().Be(50m);

        IReadOnlyList<AccountCashFlowSummary> cashFlow =
            await harness.ReportingService.GetCashFlowByAccountAsync(from, to);
        cashFlow.Should().OnlyContain(row =>
            row.AccountCode == LedgerAccounts.Expense(ExpenseCategories.Maintenance)
            || row.AccountCode == LedgerAccounts.Cash);
        cashFlow.Sum(r => r.DebitTotalPaisa).Should().Be(150_00);
    }

    [Fact]
    public async Task GetMaintenanceSpendAsync_ShouldUseLedgerNotShiftExpenseMax()
    {
        await using ReportingHarness harness = await ReportingHarness.CreateAsync();

        long spend = await harness.ReportingService.GetMaintenanceSpendAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        spend.Should().Be(150_00);
    }

    private sealed class ReportingHarness : IAsyncDisposable
    {
        private readonly string _databaseName;

        private ReportingHarness(
            WebPosDbContext context,
            IReportingService reportingService,
            Guid tenantId,
            string databaseName)
        {
            Context = context;
            ReportingService = reportingService;
            TenantId = tenantId;
            _databaseName = databaseName;
        }

        public WebPosDbContext Context { get; }

        public IReportingService ReportingService { get; }

        public Guid TenantId { get; }

        public static async Task<ReportingHarness> CreateAsync()
        {
            Guid tenantId = TenantDefaults.MasterTenantId;
            string databaseName = $"WebPos_Reporting_{Guid.NewGuid():N}";
            DbContextOptions<WebPosDbContext> options =
                new DbContextOptionsBuilder<WebPosDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options;

            var tenantService = new TestTenantService(tenantId);
            var context = new WebPosDbContext(options, tenantService);
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Master Tenant",
                Slug = $"master-{tenantId:N}",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            await SeedTenantDataAsync(context, tenantId);

            IReportingService reportingService = new ReportingService(
                new TestDbContextFactory(options, tenantService),
                tenantService);
            return new ReportingHarness(context, reportingService, tenantId, databaseName);
        }

        private sealed class TestDbContextFactory(
            DbContextOptions<WebPosDbContext> options,
            ITenantService tenant) : IDbContextFactory<WebPosDbContext>
        {
            public WebPosDbContext CreateDbContext() => new(options, tenant);
        }

        public async Task SeedForeignTenantAsync(Guid otherTenantId)
        {
            DbContextOptions<WebPosDbContext> options =
                new DbContextOptionsBuilder<WebPosDbContext>()
                    .UseInMemoryDatabase(_databaseName)
                    .Options;

            await using var foreignContext = new WebPosDbContext(
                options,
                new TestTenantService(otherTenantId));

            foreignContext.Tenants.Add(new Tenant
            {
                Id = otherTenantId,
                Name = "Other Tenant",
                Slug = $"other-{otherTenantId:N}",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });

            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreignContext.ShiftExpenses.Add(new ShiftExpense
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                ShiftId = Guid.NewGuid(),
                ExpenseCategory = ExpenseCategories.Maintenance,
                AmountPaisa = 999_00,
                Description = "foreign",
                LoggedAt = now,
                LoggedByUserId = Guid.NewGuid(),
                PaymentMethod = "CASH",
                VoucherNo = "F-1"
            });
            foreignContext.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                TransactionGroupId = Guid.NewGuid(),
                AccountCode = LedgerAccounts.Expense(ExpenseCategories.Maintenance),
                DebitPaisa = 999_00,
                CreditPaisa = 0,
                TransactionType = "EXPENSE",
                ReferenceNo = "FOREIGN",
                CreatedAt = now
            });
            foreignContext.SalesItems.Add(new SalesItem
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                InvoiceNo = "FOREIGN-INV",
                ProductId = Guid.NewGuid(),
                BatchId = Guid.NewGuid(),
                Quantity = 99m,
                UnitPricePaisa = 999_00,
                UnitCostPaisa = 500_00,
                DiscountAppliedPaisa = 0
            });
            await foreignContext.SaveChangesAsync();
        }

        private static async Task SeedTenantDataAsync(WebPosDbContext context, Guid tenantId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid roleId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid terminalId = Guid.NewGuid();
            Guid shiftId = Guid.NewGuid();
            Guid supplierId = Guid.NewGuid();
            Guid productId = Guid.NewGuid();
            Guid batchId = Guid.NewGuid();
            string invoiceNo = "INV-RPT-1";

            context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = tenantId,
                RoleName = "Cashier",
                CreatedAt = now
            });
            context.Users.Add(new User
            {
                Id = userId,
                TenantId = tenantId,
                Username = $"rpt-{userId:N}"[..20],
                PasswordHash = "x",
                RoleId = roleId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.Terminals.Add(new Terminal
            {
                Id = terminalId,
                TenantId = tenantId,
                TerminalName = $"T-{terminalId:N}"[..20],
                MacAddress = $"M-{terminalId:N}"[..20],
                IsActive = true,
                LastSyncTime = now
            });
            context.CashierShifts.Add(new CashierShift
            {
                Id = shiftId,
                TenantId = tenantId,
                TerminalId = terminalId,
                CashierId = userId,
                OpenedAt = now,
                OpeningCashPaisa = 0,
                ExpectedCashPaisa = 0,
                DiscrepancyPaisa = 0,
                Status = "OPEN"
            });
            context.ShiftExpenses.Add(new ShiftExpense
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ShiftId = shiftId,
                ExpenseCategory = ExpenseCategories.Maintenance,
                AmountPaisa = 100_00,
                Description = "ops",
                LoggedAt = now,
                LoggedByUserId = userId,
                PaymentMethod = "CASH",
                VoucherNo = "V-1"
            });
            context.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TransactionGroupId = Guid.NewGuid(),
                AccountCode = LedgerAccounts.Expense(ExpenseCategories.Maintenance),
                DebitPaisa = 150_00,
                CreditPaisa = 0,
                TransactionType = "EXPENSE",
                ReferenceNo = "MAINT-1",
                CreatedAt = now
            });
            context.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TransactionGroupId = Guid.NewGuid(),
                AccountCode = LedgerAccounts.Cash,
                DebitPaisa = 0,
                CreditPaisa = 150_00,
                TransactionType = "EXPENSE",
                ReferenceNo = "MAINT-1",
                CreatedAt = now
            });
            context.Parties.Add(new Party
            {
                Id = supplierId,
                TenantId = tenantId,
                PartyType = PartyTypes.Supplier,
                Name = "Rpt Supplier",
                PhoneNumber = Guid.NewGuid().ToString("N")[..16],
                Address = "Depot",
                CreditLimitPaisa = 0,
                CurrentBalancePaisa = 0,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.Products.Add(new Product
            {
                Id = productId,
                TenantId = tenantId,
                Name = "Yogurt",
                Sku = $"SKU-{productId:N}"[..20],
                Barcode = $"BAR-{productId:N}"[..20],
                Brand = "WebPos",
                BaseUnit = "PCS",
                ConversionMultiplier = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.ProductBatches.Add(new ProductBatch
            {
                Id = batchId,
                TenantId = tenantId,
                ProductId = productId,
                BatchNumber = "B-RPT",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
                CostPricePaisa = 50_00,
                RetailPricePaisa = 100_00,
                InitialQty = 10,
                CurrentQty = 8,
                SupplierId = supplierId,
                CreatedAt = now
            });
            context.SalesInvoices.Add(new SalesInvoice
            {
                InvoiceNo = invoiceNo,
                TenantId = tenantId,
                CashierId = userId,
                ShiftId = shiftId,
                TerminalId = terminalId,
                TotalAmountPaisa = 200_00,
                TaxAmountPaisa = 0,
                DiscountAmountPaisa = 0,
                ReceiptNumber = "RCPT-1",
                PaymentMethod = "CASH",
                CreatedAt = now
            });
            context.SalesItems.Add(new SalesItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                InvoiceNo = invoiceNo,
                ProductId = productId,
                BatchId = batchId,
                Quantity = 2m,
                UnitPricePaisa = 100_00,
                UnitCostPaisa = 50_00,
                DiscountAppliedPaisa = 0
            });
            await context.SaveChangesAsync();
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class TestTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }
}
