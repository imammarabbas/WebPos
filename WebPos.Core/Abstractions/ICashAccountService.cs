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

    public Guid? OpenShiftId { get; init; }

    public bool HasOpenShift => OpenShiftId is not null;
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

    Task<CashBalancesSummaryDto> GetBalancesSummaryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves GL account for a payment: explicit CashAccountId wins over payment-method defaults.
    /// </summary>
    Task<CashPaymentResolution> ResolvePaymentAccountAsync(
        Guid? cashAccountId,
        string paymentMethod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the till GL account for a terminal (ensuring the till CashAccount exists).
    /// </summary>
    Task<CashPaymentResolution> ResolveTillAccountForTerminalAsync(
        Guid terminalId,
        string? terminalName = null,
        CancellationToken cancellationToken = default);
}
