using Common.Models;

namespace WebPos.WindowsTerminal.Services;

public sealed class CartLine
{
    public Guid LineId { get; init; } = Guid.NewGuid();

    public required SalesProductDto Product { get; init; }

    public decimal Quantity { get; set; }

    public string? DescriptionOverride { get; set; }

    public string? PackingOverride { get; set; }

    /// <summary>Optional cashier override of catalog retail (paisa).</summary>
    public long? UnitPriceOverridePaisa { get; set; }

    public long EffectiveUnitPricePaisa =>
        UnitPriceOverridePaisa ?? Product.UnitPricePaisa;

    public long LineTotalPaisa =>
        (long)Math.Round(EffectiveUnitPricePaisa * Quantity, MidpointRounding.AwayFromZero);
}
