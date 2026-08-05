namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Persists Receive Goods draft across Sale ↔ Purchase toggles (singleton, like CartService).
/// Cleared only on explicit Cancel or successful submit.
/// </summary>
public sealed class ReceiveDraftService
{
    private readonly List<ReceiveDraftLine> _lines = [];

    public event Action? Changed;

    public string InvoiceNo { get; set; } = string.Empty;

    public string SupplierIdText { get; set; } = string.Empty;

    public bool CreateNewSupplier { get; set; }

    public string NewSupplierName { get; set; } = string.Empty;

    public string NewSupplierPhone { get; set; } = string.Empty;

    public string BatchNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    public IReadOnlyList<ReceiveDraftLine> Lines => _lines;

    public bool HasContent =>
        _lines.Count > 0
        || !string.IsNullOrWhiteSpace(SupplierIdText)
        || CreateNewSupplier
        || !string.IsNullOrWhiteSpace(NewSupplierName);

    public void ReplaceLines(IEnumerable<ReceiveDraftLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
        Changed?.Invoke();
    }

    public void SetLines(List<ReceiveDraftLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
        Changed?.Invoke();
    }

    public void Clear()
    {
        InvoiceNo = string.Empty;
        SupplierIdText = string.Empty;
        CreateNewSupplier = false;
        NewSupplierName = string.Empty;
        NewSupplierPhone = string.Empty;
        BatchNumber = string.Empty;
        InvoiceDate = DateTime.Today;
        _lines.Clear();
        Changed?.Invoke();
    }
}

public sealed class ReceiveDraftLine
{
    public required Guid ProductId { get; init; }

    public required string ProductName { get; init; }

    public decimal Quantity { get; set; }

    public long PurchasePricePaisa { get; init; }

    public long RetailPricePaisa { get; init; }

    public bool NeedsElevation { get; init; }
}
