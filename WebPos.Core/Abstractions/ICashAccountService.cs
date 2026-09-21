using WebPos.Core.Constants;
using WebPos.Core.Models;

namespace WebPos.Core.Abstractions;

public sealed class CashAccountDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public CashAccountType Type { get; init; }

    public required string AccountCode { get; init; }

    public Guid? TerminalId { get; init; }

    public string? TerminalName { get; init; }

    public bool IsActive { get; init; }

    public bool IsSystem { get; init; }

    public int SortOrder { get; init; }

    public string? PaymentMethodKey { get; init; }
}

public sealed class CreateCashAccountRequest
{
    public required string Name { get; init; }

    public CashAccountType Type { get; init; }

    public required string AccountCode { get; init; }

    public string? PaymentMethodKey { get; init; }

    public int SortOrder { get; init; }
}

public sealed class UpdateCashAccountRequest
{
    public required string Name { get; init; }

    public bool IsActive { get; init; }

    public int SortOrder { get; init; }

    public string? PaymentMethodKey { get; init; }
}

public sealed class CashAccountCardDto
{
    public required CashAccountDto Account { get; init; }

    public long DerivedGlBalancePaisa { get; init; }

    public long TodayInPaisa { get; init; }

    public long TodayOutPaisa { get; init; }

    /// <summary>Live drawer float when an OPEN shift exists on the till terminal.</summary>
    public long? DrawerExpectedCashPaisa { get; init; }

    /// <summary>Blind physical count when recorded (never inferred from Expected).</summary>
    public long? PhysicalBlindCashPaisa { get; init; }

    public bool HasPhysicalCount => PhysicalBlindCashPaisa is not null;

    public Guid? OpenShiftId { get; init; }

    public bool HasOpenShift => OpenShiftId is not null;

    /// <summary>Available to spend = live drawer (ExpectedCash) when a shift is open; else GL.</summary>
    public long SpendablePaisa { get; init; }

    public long UnreconciledCashInPaisa { get; init; }

    public long UnreconciledCashOutPaisa { get; init; }

    /// <summary>Opening + POS sales + injections − payouts when an OPEN shift exists.</summary>
    public CashMoneyTrailDto? Trail { get; init; }
}

public sealed class CashMoneyTrailDto
{
    public long OpeningFloatPaisa { get; init; }

    public long PosSalesPaisa { get; init; }

    public long ManualInjectionsPaisa { get; init; }

    public long PayoutsPaisa { get; init; }

    public long NetAvailablePaisa { get; init; }
}

public sealed class CashBalancesSummaryDto
{
    public required IReadOnlyList<CashAccountCardDto> Accounts { get; init; }

    public long TotalLiquidityPaisa { get; init; }

    public long InTillsPaisa { get; init; }

    public long InBankPaisa { get; init; }

    public long InPettyPaisa { get; init; }

    public long InMobilePaisa { get; init; }

    public long InOwnerPaisa { get; init; }

    public long TrailOpeningFloatPaisa { get; init; }

    public long TrailPosSalesPaisa { get; init; }

    public long TrailManualInjectionsPaisa { get; init; }

    public long TrailPayoutsPaisa { get; init; }

    public long TrailNetAvailablePaisa { get; init; }
}

public sealed class CashSpendableBalanceDto
{
    public required Guid AccountId { get; init; }

    public required string AccountCode { get; init; }

    public CashAccountType Type { get; init; }

    public long GlBalancePaisa { get; init; }

    public long? DrawerExpectedCashPaisa { get; init; }

    public Guid? OpenShiftId { get; init; }

    /// <summary>Amount that may leave this account without creating a phantom deficit.</summary>
    public long SpendablePaisa { get; init; }

    public long LedgerExceedsDrawerPaisa =>
        Type == CashAccountType.Till
        && DrawerExpectedCashPaisa is long drawer
            ? Math.Max(0L, GlBalancePaisa - drawer)
            : 0L;
}

public sealed class CashPaymentResolution
{
    public required string AccountCode { get; init; }

    public Guid? CashAccountId { get; init; }

    public Guid? TerminalId { get; init; }

    public CashAccountType? Type { get; init; }

    /// <summary>
    /// When true, open-shift ExpectedCash should move with this payment account.
    /// </summary>
    public bool AffectsTillDrawer { get; init; }
}

public sealed class CashAccountLedgerEntryDto
{
    public required Guid Id { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public Guid? ShiftId { get; init; }

    public Guid? TerminalId { get; init; }

    public string? TerminalName { get; init; }

    public required string TransactionType { get; init; }

    public required string ReferenceNo { get; init; }

    public required string ReferenceDetails { get; init; }

    public long DebitPaisa { get; init; }

    public long CreditPaisa { get; init; }

    public long RunningBalancePaisa { get; init; }

    public required string Source { get; init; }

    public string? UserName { get; init; }

    public string? ShiftLabel { get; init; }

    /// <summary>Posted for normal entries; Graceful for auto-recognized physical float.</summary>
    public required string Status { get; init; }
}

public sealed class CashAccountLedgerDto
{
    public required string AccountCode { get; init; }

    public string? AccountName { get; init; }

    public Guid? CashAccountId { get; init; }

    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }

    public long OpeningBalancePaisa { get; init; }

    public long ClosingBalancePaisa { get; init; }

    public required IReadOnlyList<CashAccountLedgerEntryDto> Entries { get; init; }
}

public interface ICashAccountService
{
    Task<IReadOnlyList<CashAccountDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<CashAccountDto> CreateAsync(
        CreateCashAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<CashAccountDto> UpdateAsync(
        Guid accountId,
        UpdateCashAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<CashAccountDto> EnsureTillAccountsAsync(
        Guid terminalId,
        string terminalName,
        CancellationToken cancellationToken = default);

    Task<long> GetAccountBalancePaisaAsync(
        Guid accountId,
        DateTime? asOf = null,
        CancellationToken cancellationToken = default);

    Task<long> GetAccountBalancePaisaByCodeAsync(
        string accountCode,
        DateTime? asOf = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Spendable cash for an account: GL for non-till; live drawer ExpectedCash for an open till.
    /// </summary>
    Task<CashSpendableBalanceDto> GetSpendableBalanceAsync(
        Guid accountId,
        Guid? shiftId = null,
        CancellationToken cancellationToken = default);

    Task<CashBalancesSummaryDto> GetBalancesSummaryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves GL account for a payment: AccountCode, then CashAccountId, then payment-method defaults.
    /// </summary>
    Task<CashPaymentResolution> ResolvePaymentAccountAsync(
        Guid? cashAccountId,
        string paymentMethod,
        CancellationToken cancellationToken = default);

    Task<CashPaymentResolution> ResolvePaymentAccountAsync(
        Guid? cashAccountId,
        string? accountCode,
        string paymentMethod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Line-by-line GL statement for a cash/asset account code with running balance.
    /// </summary>
    Task<CashAccountLedgerDto> GetLedgerByAccountCodeAsync(
        string accountCode,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the till GL account for a terminal (ensuring the till CashAccount exists).
    /// </summary>
    Task<CashPaymentResolution> ResolveTillAccountForTerminalAsync(
        Guid terminalId,
        string? terminalName = null,
        CancellationToken cancellationToken = default);
}
