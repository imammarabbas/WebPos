using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Seeding;
using WebPos.Core.Services;
using WebPos.IntegrationTests.Infrastructure;
using CloseShiftRequest = Common.Models.CloseShiftRequest;
using CashVarianceReport = Common.Models.CashVarianceReport;
using StartShiftRequest = Common.Models.StartShiftRequest;
using ShiftDto = Common.Models.ShiftDto;
using SuggestedOpeningCashDto = Common.Models.SuggestedOpeningCashDto;

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

        await FundGlAsync(scope, bank.AccountCode, 50_000L);

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

        await FundGlAsync(scope, till.AccountCode, 50_000L);

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

        await FundGlAsync(scope, owner.AccountCode, 10_000L);
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

    [Theory]
    [InlineData("OWNER_CASH")]
    [InlineData("BANK")]
    [InlineData("PETTY_CASH")]
    public async Task SupplierPayment_WithAccountCode_NoShift_ShouldPostWithoutTillChange(
        string accountCode)
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto funding = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == accountCode).Subject;
        await FundGlAsync(scope, funding.AccountCode, 10_000L);
        long fundingBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(funding.Id);

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
            AmountPaisa = 2_500L,
            PaymentMethod = "CASH",
            ReferenceNo = $"AC-{Guid.NewGuid():N}"[..18],
            ShiftId = null,
            AccountCode = accountCode
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(expectedBefore);

        long fundingAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(funding.Id);
        (fundingAfter - fundingBefore).Should().Be(-2_500L);
    }

    [Fact]
    public async Task SupplierPayment_TillAccountCode_WithoutShift_AttachesOpenShift()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 50_000L, drawerPaisa: 50_000L);
        PartyDto supplier = await CreateSupplierAsync(scope);

        await scope.ProcurementService.RecordSupplierPaymentAsync(
            new RecordSupplierPaymentRequest
            {
                SupplierId = supplier.Id,
                AmountPaisa = 1_000L,
                PaymentMethod = "CASH",
                ReferenceNo = $"TILL-{Guid.NewGuid():N}"[..18],
                ShiftId = null,
                AccountCode = till.AccountCode
            });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(49_000L);
        (await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id)).Should().Be(49_000L);
    }

    [Fact]
    public async Task SupplierPayment_FromOpeningFloat_ShouldPostWithoutPriorCashIn()
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
        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        shift.OpeningCashPaisa = 190_000L;
        shift.ExpectedCashPaisa = 190_000L;
        await scope.DbContext.SaveChangesAsync();

        CashSpendableBalanceDto spendable = await scope.CashAccountService.GetSpendableBalanceAsync(
            till.Id,
            shift.Id);
        spendable.SpendablePaisa.Should().Be(190_000L);
        spendable.GlBalancePaisa.Should().Be(0L);

        PartyDto supplier = await CreateSupplierAsync(scope);
        await scope.ProcurementService.RecordSupplierPaymentAsync(new RecordSupplierPaymentRequest
        {
            SupplierId = supplier.Id,
            AmountPaisa = 50_000L,
            PaymentMethod = "CASH",
            ReferenceNo = $"FLOAT-{Guid.NewGuid():N}"[..18],
            ShiftId = seed.ShiftId,
            CashAccountId = till.Id
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(140_000L);
        (await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id)).Should().Be(0L);

        PartyDto reloaded = await scope.PartyService.GetSupplierAsync(supplier.Id);
        reloaded.CurrentBalancePaisa.Should().Be(-50_000L);
    }

    [Fact]
    public async Task GetLedgerByAccountCode_ShouldReturnOpeningRunningBalances()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto bank = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == LedgerAccounts.Bank).Subject;
        CashAccountDto owner = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == "OWNER_CASH").Subject;

        await FundGlAsync(scope, bank.AccountCode, 10_000L);

        await scope.CashTransferService.TransferAsync(
            bank.Id,
            owner.Id,
            4_000L,
            "Ledger seed transfer");

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(1);
        CashAccountLedgerDto ledger = await scope.CashAccountService.GetLedgerByAccountCodeAsync(
            "OWNER_CASH",
            from,
            to);

        ledger.AccountCode.Should().Be("OWNER_CASH");
        ledger.Entries.Should().NotBeEmpty();
        ledger.Entries.Should().Contain(e =>
            e.DebitPaisa == 4_000L && e.TransactionType == "CASH_TRANSFER");
        ledger.ClosingBalancePaisa.Should().Be(
            ledger.OpeningBalancePaisa + ledger.Entries.Sum(e => e.DebitPaisa - e.CreditPaisa));
    }

    [Fact]
    public async Task TillToOwnerTransfer_MidShift_KeepsShiftOpen()
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

        await FundGlAsync(scope, till.AccountCode, 20_000L);

        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        shift.ExpectedCashPaisa = 20_000L;
        await scope.DbContext.SaveChangesAsync();

        await scope.CashTransferService.TransferAsync(
            till.Id,
            owner.Id,
            3_000L,
            "Mid-shift drop",
            seed.ShiftId);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.Status.Should().Be("OPEN");
        shift.ClosedAt.Should().BeNull();
        shift.ExpectedCashPaisa.Should().Be(17_000L);
    }

    [Fact]
    public async Task TillTransfer_WhenLedgerExceedsDrawer_ShouldThrowInsufficient_NotFreeze()
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

        await FundGlAsync(scope, till.AccountCode, 4_831_000L);

        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        shift.ExpectedCashPaisa = 123_000L;
        await scope.DbContext.SaveChangesAsync();

        Func<Task> act = () => scope.CashTransferService.TransferAsync(
            till.Id,
            owner.Id,
            4_831_000L,
            "Ghost spend",
            seed.ShiftId);

        await act.Should().ThrowAsync<InsufficientCashBalanceException>();

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.Status.Should().Be("OPEN");
        shift.ExpectedCashPaisa.Should().Be(123_000L);
    }

    [Fact]
    public async Task TillTransfer_WithinDrawer_ShouldSucceed_WhenGlCovers()
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

        await FundGlAsync(scope, till.AccountCode, 4_831_000L);

        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        shift.ExpectedCashPaisa = 123_000L;
        await scope.DbContext.SaveChangesAsync();

        await scope.CashTransferService.TransferAsync(
            till.Id,
            owner.Id,
            123_000L,
            "Physical drop",
            seed.ShiftId);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(0);
    }

    [Fact]
    public async Task TillShortage_ThenAllowsRemainingDrawerSpend()
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

        await FundGlAsync(scope, till.AccountCode, 500_000L);

        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        shift.ExpectedCashPaisa = 100_000L;
        await scope.DbContext.SaveChangesAsync();

        await scope.CashTransferService.RecordTillShortageAsync(new RecordTillShortageRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            ShortagePaisa = 400_000L,
            Notes = "Missing cash"
        });

        long glAfterShortage = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        glAfterShortage.Should().Be(100_000L);

        await scope.CashTransferService.TransferAsync(
            till.Id,
            owner.Id,
            100_000L,
            "Drop remaining",
            seed.ShiftId);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(0);
    }

    [Fact]
    public async Task NonTillOverdraw_ShouldThrowInsufficientFunds()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto petty = await scope.CashAccountService.CreateAsync(new CreateCashAccountRequest
        {
            Name = $"Petty {Guid.NewGuid():N}"[..18],
            Type = CashAccountType.Petty,
            AccountCode = $"PT{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            SortOrder = 90
        });
        CashAccountDto owner = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == "OWNER_CASH").Subject;

        await FundGlAsync(scope, petty.AccountCode, 1_000L);

        Func<Task> act = () => scope.CashTransferService.TransferAsync(
            petty.Id,
            owner.Id,
            5_000L,
            "Overdraw");

        await act.Should().ThrowAsync<InsufficientCashBalanceException>()
            .Where(ex => ex.Message == InsufficientCashBalanceException.StandardMessage);
    }

    [Fact]
    public async Task SupplierPayment_Overdraw_ShouldThrowStandardInsufficientMessage()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto funding = await scope.CashAccountService.CreateAsync(new CreateCashAccountRequest
        {
            Name = $"Owner2 {Guid.NewGuid():N}"[..18],
            Type = CashAccountType.Owner,
            AccountCode = $"OW{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            SortOrder = 95
        });
        await FundGlAsync(scope, funding.AccountCode, 2_000L);

        PartyDto supplier = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Supplier,
            Name = $"Sup-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 0
        });

        Func<Task> act = () => scope.ProcurementService.RecordSupplierPaymentAsync(
            new RecordSupplierPaymentRequest
            {
                SupplierId = supplier.Id,
                AmountPaisa = 5_000L,
                PaymentMethod = "CASH",
                ReferenceNo = $"REF-{Guid.NewGuid():N}"[..16],
                AccountCode = funding.AccountCode
            });

        await act.Should().ThrowAsync<InsufficientCashBalanceException>()
            .Where(ex => ex.Message == InsufficientCashBalanceException.StandardMessage);
    }

    [Fact]
    public async Task PostBalancedEntries_ShouldUpdateCashAccountBalancePaisa()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto bank = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == LedgerAccounts.Bank).Subject;

        await FundGlAsync(scope, bank.AccountCode, 7_500L);

        long balance = await scope.CashAccountService.GetAccountBalancePaisaAsync(bank.Id);
        balance.Should().BeGreaterThanOrEqualTo(7_500L);

        CashAccount row = await scope.DbContext.CashAccounts.AsNoTracking()
            .SingleAsync(a => a.Id == bank.Id);
        row.BalancePaisa.Should().Be(balance);
    }

    [Fact]
    public async Task CashIn_WhenLedgerExceedsDrawer_RaisesAvailable_WithoutGlDebit()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 587_000L, drawerPaisa: 76_000L);

        long glBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        const long cashIn = 511_000L;

        TillCashMovementResult result = await scope.CashTransferService.RecordTillCashInAsync(
            new RecordTillCashInRequest
            {
                ShiftId = seed.ShiftId,
                TillCashAccountId = till.Id,
                AmountPaisa = cashIn,
                Reason = ShiftCashInReasons.UnregisteredCash
            });

        result.AlignPaisa.Should().Be(cashIn);
        result.ExcessPaisa.Should().Be(0);
        result.Status.Should().Be(ShiftCashMovementStatuses.Unreconciled);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(587_000L);

        long glAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        glAfter.Should().Be(glBefore);

        CashSpendable.ForTill(glAfter, shift.ExpectedCashPaisa).Should().Be(587_000L);
        result.TransactionGroupId.Should().BeNull();
        result.ExcessPaisa.Should().Be(0);
    }

    [Fact]
    public async Task Shortage_WhenLedgerExceedsDrawer_DoesNotFreeze_AndIsNotCashIn()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 500_000L, drawerPaisa: 100_000L);

        await scope.CashTransferService.RecordTillShortageAsync(new RecordTillShortageRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            ShortagePaisa = 50_000L,
            Notes = "Partial shortage"
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.Status.Should().Be("OPEN");
        shift.ExpectedCashPaisa.Should().Be(100_000L);

        IReadOnlyList<TillCashMovementDto> unrec =
            await scope.CashTransferService.ListTillCashMovementsAsync(
                shiftId: seed.ShiftId,
                unreconciledOnly: true);
        unrec.Should().Contain(m => m.Reason == "Cash Shortage");
        unrec.Should().NotContain(m => m.Direction == ShiftCashMovementDirections.In);

        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 50_000L,
            Reason = ShiftCashInReasons.UnregisteredCash
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(150_000L);
        shift.Status.Should().Be("OPEN");
    }

    [Fact]
    public async Task CashIn_WhenLedgerLessThanDrawer_DebitsTillOnce_AndRaisesAvailable()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 50_000L, drawerPaisa: 80_000L);

        long glBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        const long cashIn = 20_000L;

        TillCashMovementResult result = await scope.CashTransferService.RecordTillCashInAsync(
            new RecordTillCashInRequest
            {
                ShiftId = seed.ShiftId,
                TillCashAccountId = till.Id,
                AmountPaisa = cashIn,
                Reason = ShiftCashInReasons.OwnerCashAdded
            });

        result.AlignPaisa.Should().Be(0);
        result.ExcessPaisa.Should().Be(cashIn);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(100_000L);

        long glAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        (glAfter - glBefore).Should().Be(cashIn);
        // Available follows the live drawer (100k), not min(GL, drawer).
        CashSpendable.ForTill(glAfter, shift.ExpectedCashPaisa).Should().Be(100_000L);

        List<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.TransactionType == "CASH_IN" && e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();
        legs.Should().Contain(e => e.AccountCode == till.AccountCode && e.DebitPaisa == cashIn);
        legs.Should().Contain(e =>
            e.AccountCode == LedgerAccounts.UnregisteredCash && e.CreditPaisa == cashIn);
    }

    [Fact]
    public async Task CashIn_ThenSupplierPayment_FromTill_DecreasesAvailable()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 200_000L, drawerPaisa: 50_000L);

        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 150_000L,
            Reason = ShiftCashInReasons.UnregisteredCash
        });

        PartyDto supplier = await CreateSupplierAsync(scope);
        const long pay = 40_000L;
        await scope.ProcurementService.RecordSupplierPaymentAsync(new RecordSupplierPaymentRequest
        {
            SupplierId = supplier.Id,
            AmountPaisa = pay,
            PaymentMethod = "CASH",
            ReferenceNo = $"TIN-{Guid.NewGuid():N}"[..18],
            ShiftId = seed.ShiftId,
            CashAccountId = till.Id
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(200_000L - pay);
        long gl = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        CashSpendable.ForTill(gl, shift.ExpectedCashPaisa).Should().Be(160_000L);
    }

    [Fact]
    public async Task CashOut_ThenSupplierPayment_LimitsFurtherSpend()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 100_000L, drawerPaisa: 100_000L);

        await scope.CashTransferService.RecordTillCashOutAsync(new RecordTillCashOutRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 60_000L,
            Reason = ShiftCashOutReasons.OwnerWithdrawal
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(40_000L);
        long gl = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        CashSpendable.ForTill(gl, shift.ExpectedCashPaisa).Should().Be(40_000L);

        PartyDto supplier = await CreateSupplierAsync(scope);
        Func<Task> overpay = () => scope.ProcurementService.RecordSupplierPaymentAsync(
            new RecordSupplierPaymentRequest
            {
                SupplierId = supplier.Id,
                AmountPaisa = 50_000L,
                PaymentMethod = "CASH",
                ReferenceNo = $"TOUT-{Guid.NewGuid():N}"[..18],
                ShiftId = seed.ShiftId,
                CashAccountId = till.Id
            });

        await overpay.Should().ThrowAsync<InsufficientCashBalanceException>();

        await scope.ProcurementService.RecordSupplierPaymentAsync(new RecordSupplierPaymentRequest
        {
            SupplierId = supplier.Id,
            AmountPaisa = 30_000L,
            PaymentMethod = "CASH",
            ReferenceNo = $"TOK-{Guid.NewGuid():N}"[..18],
            ShiftId = seed.ShiftId,
            CashAccountId = till.Id
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(10_000L);
    }

    [Fact]
    public async Task Reconcile_MarksStatus_AndKeepsHistory()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) =
            await SeedTillAsync(scope, glPaisa: 100_000L, drawerPaisa: 40_000L);

        TillCashMovementResult created = await scope.CashTransferService.RecordTillCashInAsync(
            new RecordTillCashInRequest
            {
                ShiftId = seed.ShiftId,
                TillCashAccountId = till.Id,
                AmountPaisa = 60_000L,
                Reason = ShiftCashInReasons.UnregisteredCash
            });

        Guid linkId = Guid.NewGuid();
        TillCashMovementResult reconciled = await scope.CashTransferService.ReconcileTillCashMovementAsync(
            new ReconcileTillCashMovementRequest
            {
                MovementId = created.MovementId,
                LinkedReferenceType = "MANUAL",
                LinkedReferenceId = linkId
            });

        reconciled.Status.Should().Be(ShiftCashMovementStatuses.Reconciled);

        IReadOnlyList<TillCashMovementDto> all =
            await scope.CashTransferService.ListTillCashMovementsAsync(shiftId: seed.ShiftId);
        all.Should().Contain(m =>
            m.Id == created.MovementId
            && m.Status == ShiftCashMovementStatuses.Reconciled
            && m.LinkedReferenceId == linkId);

        (await scope.CashTransferService.ListTillCashMovementsAsync(
            shiftId: seed.ShiftId,
            unreconciledOnly: true)).Should().NotContain(m => m.Id == created.MovementId);
    }

    [Fact]
    public async Task Reconcile_AlignOnly_DoesNotChangeGlOrDrawer()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 100_000L, drawerPaisa: 40_000L);

        TillCashMovementResult created = await scope.CashTransferService.RecordTillCashInAsync(
            new RecordTillCashInRequest
            {
                ShiftId = seed.ShiftId,
                TillCashAccountId = till.Id,
                AmountPaisa = 60_000L,
                Reason = ShiftCashInReasons.UnregisteredCash
            });

        await scope.DbContext.Entry(shift).ReloadAsync();
        long expected = shift.ExpectedCashPaisa;
        long gl = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);

        await scope.CashTransferService.ReconcileTillCashMovementAsync(new ReconcileTillCashMovementRequest
        {
            MovementId = created.MovementId,
            LinkedReferenceType = "MANUAL",
            LinkedReferenceId = Guid.NewGuid()
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(expected);
        (await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id)).Should().Be(gl);
    }

    [Fact]
    public async Task Unreconciled_CashIn_RemainsVisible_UntilResolved()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) =
            await SeedTillAsync(scope, glPaisa: 80_000L, drawerPaisa: 20_000L);

        TillCashMovementResult created = await scope.CashTransferService.RecordTillCashInAsync(
            new RecordTillCashInRequest
            {
                ShiftId = seed.ShiftId,
                TillCashAccountId = till.Id,
                AmountPaisa = 60_000L,
                Reason = ShiftCashInReasons.FloatTopUp
            });

        CashBalancesSummaryDto summary = await scope.CashAccountService.GetBalancesSummaryAsync();
        CashAccountCardDto card = summary.Accounts.Should().ContainSingle(c => c.Account.Id == till.Id).Subject;
        card.UnreconciledCashInPaisa.Should().Be(60_000L);

        (await scope.CashTransferService.ListTillCashMovementsAsync(unreconciledOnly: true))
            .Should().Contain(m => m.Id == created.MovementId);

        await scope.CashTransferService.ReverseTillCashMovementAsync(created.MovementId);
        card = (await scope.CashAccountService.GetBalancesSummaryAsync()).Accounts
            .Should().ContainSingle(c => c.Account.Id == till.Id).Subject;
        card.UnreconciledCashInPaisa.Should().Be(0);
    }

    [Fact]
    public async Task Mismatch_DoesNotFreeze_CashDropAndCashInStillWork()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 300_000L, drawerPaisa: 100_000L);
        CashAccountDto owner = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == "OWNER_CASH").Subject;

        await scope.CashTransferService.TransferAsync(
            till.Id,
            owner.Id,
            50_000L,
            "Partial drop while mismatched",
            seed.ShiftId);

        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 100_000L,
            Reason = ShiftCashInReasons.UnregisteredCash
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.Status.Should().Be("OPEN");
        shift.ClosedAt.Should().BeNull();
        shift.ExpectedCashPaisa.Should().Be(150_000L);
    }

    [Fact]
    public async Task CashDrop_Unchanged_AfterCashInAlign()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 200_000L, drawerPaisa: 80_000L);
        CashAccountDto owner = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == "OWNER_CASH").Subject;

        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 120_000L,
            Reason = ShiftCashInReasons.UnregisteredCash
        });

        long ownerBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(owner.Id);
        const long drop = 75_000L;
        await scope.CashTransferService.TransferAsync(
            till.Id,
            owner.Id,
            drop,
            "Cash drop unchanged path",
            seed.ShiftId);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(200_000L - drop);
        (await scope.CashAccountService.GetAccountBalancePaisaAsync(owner.Id) - ownerBefore)
            .Should().Be(drop);

        IReadOnlyList<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionType == "CASH_TRANSFER" && e.ReferenceDetails == "Cash drop unchanged path")
            .ToListAsync();
        legs.Should().Contain(e => e.AccountCode == till.AccountCode && e.CreditPaisa == drop);
        legs.Should().Contain(e => e.AccountCode == owner.AccountCode && e.DebitPaisa == drop);
    }

    [Fact]
    public async Task CloseShift_WhenLedgerExceedsExpected_DoesNotRequireShortage_AndKeepsExpectedCash()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 1_437_004L, drawerPaisa: 926_002L);

        const long physical = 1_200_000L;
        CashVarianceReport report = await scope.ShiftService.CloseShiftAsync(new CloseShiftRequest
        {
            ShiftId = seed.ShiftId,
            CashierId = seed.CashierId,
            TerminalId = seed.TerminalId,
            ActualCashPaisa = physical
        });

        report.ExpectedCashPaisa.Should().Be(926_002L);
        report.PhysicalCountPaisa.Should().Be(physical);
        report.DiscrepancyPaisa.Should().Be(physical - 926_002L);
        report.ReconciliationStatus.Should().Be("OVERAGE");
        report.RegisteredLedgerPaisa.Should().BeGreaterThanOrEqualTo(1_437_004L);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(926_002L);
        shift.ActualBlindCashPaisa.Should().Be(physical);
        shift.Status.Should().Be("CLOSED");

        (await scope.DbContext.GeneralLedgerEntries.AsNoTracking()
            .CountAsync(e => e.TransactionType == "CASH_SHORTAGE"
                && e.ReferenceNo.StartsWith("CLOSE-SHORT"))).Should().Be(0);
    }

    [Fact]
    public async Task ForceClose_DoesNotAutoPostLedgerGapShortage()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, _, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 500_000L, drawerPaisa: 100_000L);

        long expectedBefore = shift.ExpectedCashPaisa;
        CashVarianceReport report = await scope.ShiftService.ForceCloseShiftAsync(seed.ShiftId);

        report.ExpectedCashPaisa.Should().Be(expectedBefore);
        report.DiscrepancyPaisa.Should().Be(0);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(expectedBefore);

        (await scope.DbContext.GeneralLedgerEntries.AsNoTracking()
            .CountAsync(e => e.TransactionType == "CASH_SHORTAGE"
                && e.ReferenceNo.StartsWith("FORCE-SHORT"))).Should().Be(0);
    }

    [Fact]
    public async Task CashOut_OverAvailable_Rejects_AndLeavesExpectedUnchanged()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 50_000L, drawerPaisa: 40_000L);

        Func<Task> act = () => scope.CashTransferService.RecordTillCashOutAsync(
            new RecordTillCashOutRequest
            {
                ShiftId = seed.ShiftId,
                TillCashAccountId = till.Id,
                AmountPaisa = 50_000L,
                Reason = ShiftCashOutReasons.UnregisteredPayment
            });

        await act.Should().ThrowAsync<InsufficientCashBalanceException>();
        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(40_000L);
    }

    [Fact]
    public async Task TillRentExpense_ShouldHitTillGl_NotGenericCash()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 100_000L, drawerPaisa: 100_000L);

        long tillBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        long genericBefore = await scope.CashAccountService.GetAccountBalancePaisaByCodeAsync(LedgerAccounts.Cash);

        var expenseService = new ExpenseService(
            scope.DbFactory,
            scope.Ambient,
            scope.TransactionService,
            scope.CashAccountService,
            scope.TenantService);

        RecordExpenseResult result = await expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = seed.ShiftId,
            LoggedByUserId = seed.CashierId,
            Description = "Shop rent",
            ExpenseCategory = ExpenseCategories.Rent,
            ReceiptReference = $"RENT-{Guid.NewGuid():N}"[..16],
            PaymentMethod = "CASH",
            CashAccountId = till.Id,
            AmountPaisa = 20_000L
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(80_000L);

        long tillAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        (tillBefore - tillAfter).Should().Be(20_000L);

        long genericAfter = await scope.CashAccountService.GetAccountBalancePaisaByCodeAsync(LedgerAccounts.Cash);
        genericAfter.Should().Be(genericBefore);

        CashSpendableBalanceDto spendable = await scope.CashAccountService.GetSpendableBalanceAsync(
            till.Id,
            seed.ShiftId);
        spendable.SpendablePaisa.Should().Be(80_000L);

        List<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();
        legs.Should().Contain(e =>
            e.AccountCode == LedgerAccounts.Expense(ExpenseCategories.Rent) && e.DebitPaisa == 20_000L);
        legs.Should().Contain(e =>
            e.AccountCode == till.AccountCode && e.CreditPaisa == 20_000L);
        legs.Should().NotContain(e =>
            e.AccountCode == LedgerAccounts.Cash && e.CreditPaisa == 20_000L);
    }

    [Fact]
    public async Task NextShift_OpeningFromTillGl_AvailableWithoutCashIn()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, glPaisa: 100_000L, drawerPaisa: 100_000L);

        CashVarianceReport closed = await scope.ShiftService.CloseShiftAsync(new CloseShiftRequest
        {
            ShiftId = seed.ShiftId,
            CashierId = seed.CashierId,
            TerminalId = seed.TerminalId,
            ActualCashPaisa = 100_000L
        });
        closed.ExpectedCashPaisa.Should().Be(100_000L);

        SuggestedOpeningCashDto suggestion =
            await scope.ShiftService.GetSuggestedOpeningCashAsync(seed.TerminalId);
        suggestion.SuggestedOpeningPaisa.Should().Be(100_000L);
        suggestion.TillGlPaisa.Should().Be(100_000L);

        ShiftDto next = await scope.ShiftService.StartShiftAsync(new StartShiftRequest
        {
            CashierId = seed.CashierId,
            TerminalId = seed.TerminalId,
            OpeningCashPaisa = suggestion.SuggestedOpeningPaisa
        });
        next.ShiftId.Should().NotBe(seed.ShiftId);

        CashierShift nextShift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == next.ShiftId);
        nextShift.ExpectedCashPaisa.Should().Be(100_000L);

        long tillGlAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        tillGlAfter.Should().Be(100_000L);

        CashSpendableBalanceDto spendable = await scope.CashAccountService.GetSpendableBalanceAsync(
            till.Id,
            next.ShiftId);
        spendable.SpendablePaisa.Should().Be(100_000L);

        (await scope.DbContext.ShiftCashMovements.AsNoTracking()
            .CountAsync(m => m.ShiftId == next.ShiftId)).Should().Be(0);
    }

    [Fact]
    public async Task OwnerInvestment_IncreasesAccount_NotRevenue()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto owner = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == "OWNER_CASH").Subject;
        long before = await scope.CashAccountService.GetAccountBalancePaisaAsync(owner.Id);

        CapitalFundingResult result = await scope.CashTransferService.RecordOwnerInvestmentAsync(
            new CapitalFundingRequest
            {
                CashAccountId = owner.Id,
                AmountPaisa = 25_000L,
                Note = "Owner inject"
            });

        result.TransactionType.Should().Be("OWNER_INVESTMENT");
        (await scope.CashAccountService.GetAccountBalancePaisaAsync(owner.Id) - before)
            .Should().Be(25_000L);

        List<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();
        legs.Should().Contain(e => e.AccountCode == owner.AccountCode && e.DebitPaisa == 25_000L);
        legs.Should().Contain(e => e.AccountCode == LedgerAccounts.OwnerCapital && e.CreditPaisa == 25_000L);
        legs.Should().NotContain(e => e.AccountCode == LedgerAccounts.Revenue);
    }

    [Fact]
    public async Task LoanReceived_ThenRepayment_UsesLoanLiability()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto bank = (await scope.CashAccountService.ListAsync())
            .Should().ContainSingle(a => a.AccountCode == LedgerAccounts.Bank).Subject;

        CapitalFundingResult received = await scope.CashTransferService.RecordLoanReceivedAsync(
            new CapitalFundingRequest
            {
                CashAccountId = bank.Id,
                AmountPaisa = 100_000L
            });
        received.TransactionType.Should().Be("LOAN_RECEIVED");

        CapitalFundingResult repaid = await scope.CashTransferService.RecordLoanRepaymentAsync(
            new CapitalFundingRequest
            {
                CashAccountId = bank.Id,
                AmountPaisa = 40_000L
            });
        repaid.TransactionType.Should().Be("LOAN_REPAYMENT");

        List<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.TransactionGroupId == repaid.TransactionGroupId)
            .ToListAsync();
        legs.Should().Contain(e => e.AccountCode == LedgerAccounts.LoanPayable && e.DebitPaisa == 40_000L);
        legs.Should().Contain(e => e.AccountCode == bank.AccountCode && e.CreditPaisa == 40_000L);
        legs.Should().NotContain(e => e.AccountCode.StartsWith("EXPENSE:"));
    }

    private static async Task<(SaleSeedData Seed, CashAccountDto Till, CashierShift Shift)> SeedTillAsync(
        IntegrationTestScope scope,
        long glPaisa,
        long drawerPaisa)
    {
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto till = await scope.CashAccountService.EnsureTillAccountsAsync(
            seed.TerminalId,
            "Pilot Till");
        await FundGlAsync(scope, till.AccountCode, glPaisa);

        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        shift.ExpectedCashPaisa = drawerPaisa;
        await scope.DbContext.SaveChangesAsync();
        return (seed, till, shift);
    }

    private static Task<PartyDto> CreateSupplierAsync(IntegrationTestScope scope) =>
        scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Supplier,
            Name = $"Sup-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 0
        });

    private static Task FundGlAsync(IntegrationTestScope scope, string accountCode, long amountPaisa) =>
        scope.TransactionService.ExecuteInTransactionAsync(ct =>
            scope.TransactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "TEST_FUND",
                    ReferenceNo = $"FUND-{Guid.NewGuid():N}"[..16],
                    ReferenceDetails = "Test funding",
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = accountCode,
                            DebitPaisa = amountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.Revenue,
                            DebitPaisa = 0,
                            CreditPaisa = amountPaisa
                        }
                    ]
                },
                ct));
}
