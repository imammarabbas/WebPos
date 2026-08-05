using WebPos.Core.Constants;
using WebPos.Core.Entities;

namespace WebPos.Core.Models;

/// <summary>
/// Tenant-scoped cash / wallet registry. <see cref="AccountCode"/> is posted on
/// <see cref="GeneralLedgerEntry.AccountCode"/>; balances are derived from the GL.
/// </summary>
public class CashAccount : BaseEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public CashAccountType Type { get; set; }

    /// <summary>Unique per tenant; written to general ledger legs.</summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>Required when <see cref="Type"/> is Till — links to a physical <see cref="Terminal"/>.</summary>
    public Guid? TerminalId { get; set; }

    public Terminal? Terminal { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsSystem { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Optional POS/payment mapping key (CASH, EASYPAISA, BANK_TRANSFER, …).</summary>
    public string? PaymentMethodKey { get; set; }
}
