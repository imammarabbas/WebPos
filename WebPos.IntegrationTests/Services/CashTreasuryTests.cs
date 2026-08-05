using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Seeding;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Services;

[Collection("Postgres")]
public sealed class CashTreasuryTests
{
    private readonly PostgresFixture _fixture;

    public CashTreasuryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateSecondMobile_TransferBankToMobile_ShouldMoveGlBalances()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto bank = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == LedgerAccounts.Bank).Subject;

        CashAccountDto mobile2 = await scope.CashAccountService.CreateAsync(new CreateCashAccountRequest
        {
            Name = $"EasyPaisa 2 {Guid.NewGuid():N}"[..22],
            Type = CashAccountType.Mobile,
            AccountCode = $"EP2{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            PaymentMethodKey = "EASYPAISA",
            SortOrder = 25
        });

        long bankBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(bank.Id);
        long mobileBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(mobile2.Id);

        const long amount = 12_500L;
        await scope.CashTransferService.TransferAsync(
            bank.Id,
            mobile2.Id,
            amount,
            "Fund second EasyPaisa");

        long bankAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(bank.Id);
        long mobileAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(mobile2.Id);

        (bankAfter - bankBefore).Should().Be(-amount);
        (mobileAfter - mobileBefore).Should().Be(amount);

        IReadOnlyList<CashTransferDto> transfers =
            await scope.CashTransferService.ListTransfersAsync(mobile2.Id);
        transfers.Should().Contain(t =>
            t.ToAccountId == mobile2.Id
            && t.FromAccountId == bank.Id
            && t.AmountPaisa == amount);
    }

    [Fact]
    public async Task TillToOwnerTransfer_WithOpenShift_ShouldDecreaseExpectedCash()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto till = await scope.CashAccountService.EnsureTillAccountsAsync(
            seed.TerminalId,
            "Pilot Till");
        CashAccountDto owner = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == "OWNER_CASH").Subject;

        long ownerBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(owner.Id);

        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        shift.ExpectedCashPaisa = 50_000L;
        await scope.DbContext.SaveChangesAsync();

        const long amount = 7_500L;
        await scope.CashTransferService.TransferAsync(
            till.Id,
            owner.Id,
            amount,
            "Cash to owner",
            seed.ShiftId);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(50_000L - amount);

        long ownerAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(owner.Id);
        (ownerAfter - ownerBefore).Should().Be(amount);

        IReadOnlyList<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionType == "CASH_TRANSFER" && e.ReferenceDetails == "Cash to owner")
            .ToListAsync();
        legs.Should().Contain(e =>
            e.AccountCode == owner.AccountCode && e.DebitPaisa == amount);
        legs.Should().Contain(e =>
            e.AccountCode == till.AccountCode && e.CreditPaisa == amount);
    }

    [Fact]
    public async Task SupplierPayment_FromOwnerCashAccount_ShouldNotChangeTillExpected()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto owner = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == "OWNER_CASH").Subject;

        long ownerBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(owner.Id);

        PartyDto supplier = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Supplier,
            Name = $"Sup-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 0
        });

        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        long expectedBefore = shift.ExpectedCashPaisa;

        await scope.ProcurementService.RecordSupplierPaymentAsync(new RecordSupplierPaymentRequest
        {
            SupplierId = supplier.Id,
            AmountPaisa = 3_000L,
            PaymentMethod = "CASH",
            ReferenceNo = $"OWN-{Guid.NewGuid():N}"[..18],
            ShiftId = seed.ShiftId,
            CashAccountId = owner.Id
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(expectedBefore);

        long ownerAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(owner.Id);
        (ownerAfter - ownerBefore).Should().Be(-3_000L);
    }

    [Fact]
    public async Task CashSale_OnTerminal_ShouldPostToTillSpecificAccountCode()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        string expectedTillCode = CashAccountSeeder.BuildTillAccountCode(seed.TerminalId);
        CashAccountDto till = await scope.CashAccountService.EnsureTillAccountsAsync(
            seed.TerminalId,
            "POS");

        till.AccountCode.Should().Be(expectedTillCode);

        string invoiceNo = $"INV-{Guid.NewGuid():N}";
        await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = 1m,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        });

        GeneralLedgerEntry paymentLeg = await scope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.ReferenceNo == invoiceNo
                && e.TransactionType == "SALE"
                && e.DebitPaisa > 0
                && !e.AccountCode.StartsWith("EXPENSE:"))
            .OrderByDescending(e => e.CreatedAt)
            .FirstAsync();

        paymentLeg.AccountCode.Should().Be(expectedTillCode);
        paymentLeg.AccountCode.Should().NotBe(LedgerAccounts.Cash);
    }
}
