using WebPos.WindowsTerminal.Services;

namespace WebPos.WindowsTerminal.Tests;

public class PaymentBreakdownCalculatorTests
{
    [Fact]
    public void WalkIn_Underpay_BlocksConfirm_NoChange()
    {
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: true,
            previousDuePaisa: 999_00,
            currentSaleTotalPaisa: 426_00,
            paymentReceivedPaisa: 100_00,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.True(b.IsWalkIn);
        Assert.Equal(0, b.PreviousDuePaisa);
        Assert.Equal(426_00, b.AmountDuePaisa);
        Assert.False(b.CanConfirm);
        Assert.Equal(0, b.ChangeReturnPaisa);
        Assert.Equal(0, b.AccountCreditPaisa);
    }

    [Fact]
    public void WalkIn_Overpay_ReturnsChange()
    {
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: true,
            previousDuePaisa: 0,
            currentSaleTotalPaisa: 426_00,
            paymentReceivedPaisa: 500_00,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.True(b.CanConfirm);
        Assert.Equal(74_00, b.ChangeReturnPaisa);
        Assert.Equal(PaymentBreakdownCalculator.ChangeToReturnLabel, b.ChangeLabel);
        Assert.Equal(0, b.AccountCreditPaisa);
    }

    [Fact]
    public void Registered_PartialPayment_AllowsConfirm_AndPreviewsBalance()
    {
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: false,
            previousDuePaisa: 1_000_000,
            currentSaleTotalPaisa: 42_600,
            paymentReceivedPaisa: 500_000,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.Equal(1_042_600, b.AmountDuePaisa);
        Assert.True(b.CanConfirm);
        Assert.Equal(0, b.ChangeReturnPaisa);
        Assert.Equal(0, b.AccountCreditPaisa);
        Assert.Equal(542_600, b.NewCustomerBalancePaisa);
    }

    [Fact]
    public void Registered_ExactTotalLiability_ZeroBalance_NoChange()
    {
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: false,
            previousDuePaisa: 1_000_000,
            currentSaleTotalPaisa: 42_600,
            paymentReceivedPaisa: 1_042_600,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.True(b.CanConfirm);
        Assert.Equal(0, b.ChangeReturnPaisa);
        Assert.Equal(0, b.AccountCreditPaisa);
        Assert.Equal(0, b.NewCustomerBalancePaisa);
    }

    [Fact]
    public void Registered_Overpay_CreditsAccount_NotChange()
    {
        // Sale 200, pay 1000 → 800 to account
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: false,
            previousDuePaisa: 0,
            currentSaleTotalPaisa: 20_000,
            paymentReceivedPaisa: 100_000,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.True(b.CanConfirm);
        Assert.Equal(0, b.ChangeReturnPaisa);
        Assert.Equal(80_000, b.AccountCreditPaisa);
        Assert.Equal(-80_000, b.NewCustomerBalancePaisa);
        Assert.True(b.ApplyExcessAsCustomerCredit);
        Assert.Equal(PaymentBreakdownCalculator.NewBalanceLabel, b.ChangeLabel);
    }

    [Fact]
    public void Registered_Balance5390_Pay6200_Credits810()
    {
        // Amount Due = Previous Due 5390 (no extra sale) → pay 6200 → credit 810
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: false,
            previousDuePaisa: 539_000,
            currentSaleTotalPaisa: 0,
            paymentReceivedPaisa: 620_000,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.Equal(539_000, b.AmountDuePaisa);
        Assert.Equal(81_000, b.AccountCreditPaisa);
        Assert.Equal(-81_000, b.NewCustomerBalancePaisa);
        Assert.True(b.ApplyExcessAsCustomerCredit);
    }

    [Fact]
    public void Registered_Balance5390_PlusSale810_Pay6200_SettlesExact()
    {
        // Previous Due 5390 + Current Sale 810 = Amount Due 6200; pay 6200 → no credit
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: false,
            previousDuePaisa: 539_000,
            currentSaleTotalPaisa: 81_000,
            paymentReceivedPaisa: 620_000,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.Equal(620_000, b.AmountDuePaisa);
        Assert.Equal(0, b.AccountCreditPaisa);
        Assert.Equal(0, b.NewCustomerBalancePaisa);
        Assert.False(b.ApplyExcessAsCustomerCredit);
    }

    [Fact]
    public void Registered_Prev559007_Sale200_Pay8000_Credits220993()
    {
        // Screenshot case: due 5790.07, pay 8000 → credit 2209.93
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: false,
            previousDuePaisa: 559_007,
            currentSaleTotalPaisa: 20_000,
            paymentReceivedPaisa: 800_000,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.Equal(579_007, b.AmountDuePaisa);
        Assert.Equal(0, b.ChangeReturnPaisa);
        Assert.Equal(220_993, b.AccountCreditPaisa);
        Assert.Equal(-220_993, b.NewCustomerBalancePaisa);
        Assert.True(b.ApplyExcessAsCustomerCredit);
    }

    [Fact]
    public void Registered_OverpayBeyondDues_CreditsAccount()
    {
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: false,
            previousDuePaisa: 1_000_000,
            currentSaleTotalPaisa: 42_600,
            paymentReceivedPaisa: 1_052_600,
            mode: PaymentBreakdownMode.Cash,
            applyExcessAsCustomerCredit: false);

        Assert.True(b.CanConfirm);
        Assert.Equal(0, b.ChangeReturnPaisa);
        Assert.Equal(10_000, b.AccountCreditPaisa);
        Assert.Equal(-10_000, b.NewCustomerBalancePaisa);
        Assert.True(b.ApplyExcessAsCustomerCredit);
    }

    [Fact]
    public void Registered_CreditMode_OnAccount()
    {
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: false,
            previousDuePaisa: 1_000_000,
            currentSaleTotalPaisa: 42_600,
            paymentReceivedPaisa: 999_00,
            mode: PaymentBreakdownMode.Credit,
            applyExcessAsCustomerCredit: false);

        Assert.True(b.CanConfirm);
        Assert.Equal(0, b.PaymentReceivedPaisa);
        Assert.Equal(42_600, b.OnAccountPaisa);
        Assert.Equal(1_042_600, b.NewCustomerBalancePaisa);
        Assert.Equal("On Account", b.ChangeLabel);
    }

    [Fact]
    public void WalkIn_CreditMode_Blocked()
    {
        PaymentBreakdown b = PaymentBreakdownCalculator.Compute(
            isWalkIn: true,
            previousDuePaisa: 0,
            currentSaleTotalPaisa: 42_600,
            paymentReceivedPaisa: 0,
            mode: PaymentBreakdownMode.Credit,
            applyExcessAsCustomerCredit: false);

        Assert.False(b.CanConfirm);
        Assert.Equal(0, b.OnAccountPaisa);
    }
}
