using Common.Models;
using WebPos.WindowsTerminal.Services;

namespace WebPos.WindowsTerminal.Tests;

public class CartPriceOverrideTests
{
    private const long CatalogPricePaisa = 10_000L; // Rs 100
    private const long OverridePricePaisa = 9_000L; // Rs 90

    [Fact]
    public void Override_Changes_Only_That_Line_Effective_Price()
    {
        CartService cart = new();
        SalesProductDto product = CreateProduct(CatalogPricePaisa);
        cart.Add(product, 1m);

        Assert.True(cart.SetUnitPrice(product.ProductId, OverridePricePaisa));

        CartLine line = Assert.Single(cart.Lines);
        Assert.Equal(OverridePricePaisa, line.UnitPriceOverridePaisa);
        Assert.Equal(OverridePricePaisa, line.EffectiveUnitPricePaisa);
        Assert.Equal(CatalogPricePaisa, line.Product.UnitPricePaisa);
        Assert.Equal(OverridePricePaisa, line.LineTotalPaisa);
        Assert.Equal(OverridePricePaisa, cart.SubtotalPaisa);
    }

    [Fact]
    public void Clearing_Override_Restores_Catalog_Price()
    {
        CartService cart = new();
        SalesProductDto product = CreateProduct(CatalogPricePaisa);
        cart.Add(product, 1m);
        cart.SetUnitPrice(product.ProductId, OverridePricePaisa);

        Assert.True(cart.SetUnitPrice(product.ProductId, CatalogPricePaisa));

        CartLine line = Assert.Single(cart.Lines);
        Assert.Null(line.UnitPriceOverridePaisa);
        Assert.Equal(CatalogPricePaisa, line.EffectiveUnitPricePaisa);
    }

    [Fact]
    public void Quantity_Multiplies_Override_Line_Total()
    {
        CartService cart = new();
        SalesProductDto product = CreateProduct(CatalogPricePaisa);
        cart.Add(product, 5m);
        cart.SetUnitPrice(product.ProductId, OverridePricePaisa);

        CartLine line = Assert.Single(cart.Lines);
        Assert.Equal(45_000L, line.LineTotalPaisa); // 90 * 5
        Assert.Equal(45_000L, cart.SubtotalPaisa);
    }

    [Fact]
    public void Negative_Price_Is_Rejected()
    {
        CartService cart = new();
        SalesProductDto product = CreateProduct(CatalogPricePaisa);
        cart.Add(product, 1m);

        Assert.False(cart.SetUnitPrice(product.ProductId, -1));

        CartLine line = Assert.Single(cart.Lines);
        Assert.Null(line.UnitPriceOverridePaisa);
        Assert.Equal(CatalogPricePaisa, line.EffectiveUnitPricePaisa);
        Assert.Equal(CatalogPricePaisa, cart.SubtotalPaisa);
    }

    [Fact]
    public void Override_On_One_Product_Does_Not_Affect_Another()
    {
        CartService cart = new();
        SalesProductDto a = CreateProduct(CatalogPricePaisa, "A");
        SalesProductDto b = CreateProduct(20_000L, "B");
        cart.Add(a, 1m);
        cart.Add(b, 1m);

        Assert.True(cart.SetUnitPrice(a.ProductId, OverridePricePaisa));

        CartLine lineA = Assert.Single(cart.Lines, l => l.Product.ProductId == a.ProductId);
        CartLine lineB = Assert.Single(cart.Lines, l => l.Product.ProductId == b.ProductId);
        Assert.Equal(OverridePricePaisa, lineA.EffectiveUnitPricePaisa);
        Assert.Equal(20_000L, lineB.EffectiveUnitPricePaisa);
        Assert.Equal(OverridePricePaisa + 20_000L, cart.SubtotalPaisa);
    }

    [Fact]
    public void No_Override_Keeps_Catalog_Price()
    {
        CartService cart = new();
        SalesProductDto product = CreateProduct(CatalogPricePaisa);
        cart.Add(product, 1m);

        CartLine line = Assert.Single(cart.Lines);
        Assert.Null(line.UnitPriceOverridePaisa);
        Assert.Equal(CatalogPricePaisa, line.EffectiveUnitPricePaisa);
    }

    private static SalesProductDto CreateProduct(long unitPricePaisa, string? suffix = null)
    {
        Guid id = Guid.NewGuid();
        string tag = suffix ?? "X";
        return new SalesProductDto
        {
            ProductId = id,
            BatchId = Guid.Empty,
            BatchNumber = string.Empty,
            Name = $"Product {tag}",
            Sku = $"SKU-{tag}",
            Barcode = $"BC-{tag}-{id:N}"[..20],
            ShortCode = "1001",
            CategoryName = "Test",
            UnitPricePaisa = unitPricePaisa,
            AvailableStock = 100m
        };
    }
}
