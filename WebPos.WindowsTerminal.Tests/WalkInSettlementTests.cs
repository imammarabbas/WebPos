using WebPos.WindowsTerminal.Services;

namespace WebPos.WindowsTerminal.Tests;

public class WalkInSettlementTests
{
    [Fact]
    public void WalkIn_ZeroTender_ResolvesToNull()
    {
        long? amount = WalkInSettlement.ResolveAmountPaidForApi(
            isWalkIn: true,
            isCreditMode: false,
            uiAmountPaidPaisa: 0);

        Assert.Null(amount);
    }

    [Fact]
    public void WalkIn_FullTender_PassesThrough()
    {
        long? amount = WalkInSettlement.ResolveAmountPaidForApi(
            isWalkIn: true,
            isCreditMode: false,
            uiAmountPaidPaisa: 250_000);

        Assert.Equal(250_000, amount);
    }

    [Fact]
    public void Registered_ZeroTender_PassesZero()
    {
        long? amount = WalkInSettlement.ResolveAmountPaidForApi(
            isWalkIn: false,
            isCreditMode: false,
            uiAmountPaidPaisa: 0);

        Assert.Equal(0, amount);
    }

    [Fact]
    public void CreditMode_AlwaysNull()
    {
        long? walkIn = WalkInSettlement.ResolveAmountPaidForApi(
            isWalkIn: true,
            isCreditMode: true,
            uiAmountPaidPaisa: 0);
        long? registered = WalkInSettlement.ResolveAmountPaidForApi(
            isWalkIn: false,
            isCreditMode: true,
            uiAmountPaidPaisa: 100);

        Assert.Null(walkIn);
        Assert.Null(registered);
    }
}
