using Common.Models;

namespace WebPos.WindowsTerminal.Services;

public sealed class CartLine
{
    public required SalesProductDto Product { get; init; }

    public decimal Quantity { get; set; }

    public long LineTotalPaisa =>
        (long)Math.Round(Product.UnitPricePaisa * Quantity, MidpointRounding.AwayFromZero);
}
