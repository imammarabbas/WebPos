namespace WebPos.Core;

/// <summary>
/// Converts purchase quantities into stock (sale) units.
/// Stock is always kept in the product's sale / base unit.
/// </summary>
public static class UnitConversion
{
    public static int NormalizeMultiplier(int conversionMultiplier) =>
        conversionMultiplier <= 0 ? 1 : conversionMultiplier;

    /// <summary>
    /// Purchase qty (purchase unit) → stock qty (sale unit).
    /// Example: 2.5 kg × 1000 = 2500 g.
    /// </summary>
    public static decimal ToStockQty(decimal purchaseQty, int conversionMultiplier) =>
        purchaseQty * NormalizeMultiplier(conversionMultiplier);

    /// <summary>
    /// Price per purchase unit → price per sale/stock unit (paisa).
    /// Example: Rs 200/kg ÷ 1000 = Rs 0.20/g.
    /// </summary>
    public static long ToStockUnitPricePaisa(long purchaseUnitPricePaisa, int conversionMultiplier)
    {
        int m = NormalizeMultiplier(conversionMultiplier);
        if (m == 1)
        {
            return purchaseUnitPricePaisa;
        }

        return (long)Math.Round(purchaseUnitPricePaisa / (decimal)m, MidpointRounding.AwayFromZero);
    }

    public static string EffectivePurchaseUnit(string? purchaseUnit, string? saleUnit)
    {
        if (!string.IsNullOrWhiteSpace(purchaseUnit))
        {
            return purchaseUnit.Trim();
        }

        return string.IsNullOrWhiteSpace(saleUnit) ? "PCS" : saleUnit.Trim();
    }
}
