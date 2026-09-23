using Common.Models;

namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// In-memory POS screen snapshot so leaving Sales and coming back
/// restores search text, selection, draft editors, and cursor target.
/// Frontend UI only — not an API or business-logic service.
/// </summary>
public sealed class PosUiStateStore
{
    public static PosUiStateStore Shared { get; } = new();

    public PosUiSnapshot? Snapshot { get; private set; }

    public bool HasSnapshot => Snapshot is not null;

    public void Save(PosUiSnapshot snapshot) => Snapshot = snapshot;
}

public sealed class PosUiSnapshot
{
    public string ScanText { get; init; } = string.Empty;

    public bool BroadSearch { get; init; }

    public bool SearchOpen { get; init; }

    public int ScanHighlight { get; init; }

    public int SelectedLineIndex { get; init; } = -1;

    public string? FocusedField { get; init; }

    public Guid? LastScannedLineId { get; init; }

    public string DiscountRsText { get; init; } = string.Empty;

    public string DiscountPctText { get; init; } = string.Empty;

    public string CashGivenText { get; init; } = string.Empty;

    public string Remarks { get; init; } = string.Empty;

    public string DraftInvoiceNo { get; init; } = string.Empty;

    public DateTime SaleDate { get; init; }

    public string OnlineChannel { get; init; } = string.Empty;

    public string OnlineTxnRef { get; init; } = string.Empty;

    public string WhatsAppPhone { get; init; } = string.Empty;

    public bool WhatsAppReceipt { get; init; }

    public bool PrintOnPrinter { get; init; }

    public bool UseWalkInCustomer { get; init; } = true;

    public PartyDto? SelectedCustomer { get; init; }

    public int PaymentMode { get; init; }

    public Dictionary<Guid, string> QtyText { get; init; } = [];

    public Dictionary<Guid, string> PriceText { get; init; } = [];

    public Dictionary<Guid, string> TotalText { get; init; } = [];

    public Dictionary<Guid, string> CodeText { get; init; } = [];
}
