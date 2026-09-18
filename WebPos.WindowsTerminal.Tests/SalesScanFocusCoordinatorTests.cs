using Common.Models;
using WebPos.WindowsTerminal.Services;

namespace WebPos.WindowsTerminal.Tests;

public sealed class SalesScanFocusCoordinatorTests
{
    [Fact]
    public void Scanner_workflow_keeps_focus_on_scan_until_tab_to_latest_qty()
    {
        var focus = new SalesScanFocusCoordinator();

        // Open Sales → barcode focused
        focus.RequestScan();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);

        // Scan A, B, C — each returns to barcode (never qty)
        focus.OnProductAdded();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);

        focus.OnProductAdded();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);

        focus.OnProductAdded();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);

        // TAB → latest row quantity (Product C = index 2)
        focus.RequestLatestQty(lineCount: 3);
        (SalesScanFocusCoordinator.Target target, int? qtyRow) afterTab = focus.TakePending();
        Assert.Equal(SalesScanFocusCoordinator.Target.Qty, afterTab.target);
        Assert.Equal(2, afterTab.qtyRow);

        // Change qty → ENTER → barcode
        focus.OnQtyEnter();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);

        // Scan D → barcode again
        focus.OnProductAdded();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);

        // Repeat TAB on 4 lines → latest index 3
        focus.RequestLatestQty(4);
        Assert.Equal(3, focus.TakePending().qtyRow);
    }

    [Fact]
    public void Tab_with_empty_cart_falls_back_to_scan()
    {
        var focus = new SalesScanFocusCoordinator();
        focus.RequestLatestQty(0);
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);
    }

    [Fact]
    public void Not_found_and_row_removed_return_to_scan()
    {
        var focus = new SalesScanFocusCoordinator();

        focus.OnProductNotFound();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);

        focus.OnRowRemoved();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);
    }

    [Fact]
    public void Duplicate_scan_merge_still_returns_to_scan_and_tab_uses_latest_row()
    {
        var cart = new CartService();
        SalesProductDto a = CreateProduct("A");
        SalesProductDto b = CreateProduct("B");

        cart.Add(a);
        cart.Add(b);
        cart.Add(a); // merge into first row — still 2 lines

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(2m, cart.Lines[0].Quantity);

        var focus = new SalesScanFocusCoordinator();
        focus.OnProductAdded();
        Assert.Equal(SalesScanFocusCoordinator.Target.Scan, focus.TakePending().target);

        focus.RequestLatestQty(cart.Lines.Count);
        Assert.Equal(1, focus.TakePending().qtyRow); // latest = B, not first row
    }

    private static SalesProductDto CreateProduct(string code) => new()
    {
        ProductId = Guid.NewGuid(),
        BatchId = Guid.NewGuid(),
        BatchNumber = "B1",
        Name = code,
        Sku = code,
        Barcode = code,
        ShortCode = code,
        CategoryName = "General",
        UnitPricePaisa = 100,
        AvailableStock = 100
    };
}
