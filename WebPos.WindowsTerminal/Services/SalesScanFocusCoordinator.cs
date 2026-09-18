namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Scanner-first focus policy for the Sales/POS screen.
/// After a product is added, focus stays on barcode/search.
/// TAB moves to the latest cart qty; ENTER from qty returns to barcode.
/// </summary>
public sealed class SalesScanFocusCoordinator
{
    public enum Target
    {
        None,
        Scan,
        Qty
    }

    public Target Pending { get; private set; } = Target.None;

    public int? PendingQtyRow { get; private set; }

    public void RequestScan()
    {
        Pending = Target.Scan;
        PendingQtyRow = null;
    }

    public void RequestLatestQty(int lineCount)
    {
        if (lineCount <= 0)
        {
            RequestScan();
            return;
        }

        Pending = Target.Qty;
        PendingQtyRow = lineCount - 1;
    }

    public void OnProductAdded() => RequestScan();

    public void OnQtyEnter() => RequestScan();

    public void OnProductNotFound() => RequestScan();

    public void OnRowRemoved() => RequestScan();

    /// <summary>
    /// Consumes the pending focus request after a Blazor render.
    /// </summary>
    public (Target target, int? qtyRow) TakePending()
    {
        Target target = Pending;
        int? qtyRow = PendingQtyRow;
        Pending = Target.None;
        PendingQtyRow = null;
        return (target, qtyRow);
    }
}
