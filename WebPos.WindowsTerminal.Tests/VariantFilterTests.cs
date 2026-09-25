using Common.Models;
using WebPos.WindowsTerminal.Services;

namespace WebPos.WindowsTerminal.Tests;

public sealed class VariantFilterTests
{
    private static SaleVariantChildDto Child(
        string packing,
        string shortCode,
        string sku,
        string barcode,
        string name = "Chana Dal") =>
        new()
        {
            ProductId = Guid.NewGuid(),
            Name = name,
            PackingSize = packing,
            ShortCode = shortCode,
            Sku = sku,
            Barcode = barcode,
            UnitPricePaisa = 30000,
            AvailableStock = 10
        };

    private static IReadOnlyList<SaleVariantChildDto> Sample() =>
    [
        Child("1kg", "c200", "SKU-AAA", "2260911121041"),
        Child("500g", "c500", "SKU-6dfd19558996", "2260910040134")
    ];

    [Fact]
    public void Filter_c200_excludes_500g_row()
    {
        IReadOnlyList<SaleVariantChildDto> filtered = VariantFilter.Filter(Sample(), "c200");

        Assert.Single(filtered);
        Assert.Equal("1kg", filtered[0].PackingSize);
        Assert.Equal("c200", filtered[0].ShortCode);
        Assert.DoesNotContain(filtered, c => c.PackingSize == "500g");
    }

    [Fact]
    public void Filter_empty_query_returns_all()
    {
        IReadOnlyList<SaleVariantChildDto> source = Sample();
        IReadOnlyList<SaleVariantChildDto> filtered = VariantFilter.Filter(source, "");

        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void Filter_500g_keeps_only_matching_packing()
    {
        IReadOnlyList<SaleVariantChildDto> filtered = VariantFilter.Filter(Sample(), "500g");

        Assert.Single(filtered);
        Assert.Equal("500g", filtered[0].PackingSize);
    }

    [Fact]
    public void Filter_partial_sku_token_match()
    {
        IReadOnlyList<SaleVariantChildDto> filtered = VariantFilter.Filter(Sample(), "6dfd195");

        Assert.Single(filtered);
        Assert.Contains("6dfd195", filtered[0].Sku, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filter_exact_barcode()
    {
        IReadOnlyList<SaleVariantChildDto> filtered =
            VariantFilter.Filter(Sample(), "2260911121041");

        Assert.Single(filtered);
        Assert.Equal("2260911121041", filtered[0].Barcode);
    }

    [Fact]
    public void Filter_no_match_returns_empty()
    {
        IReadOnlyList<SaleVariantChildDto> filtered = VariantFilter.Filter(Sample(), "zzzz-nomatch");

        Assert.Empty(filtered);
    }

    [Fact]
    public void Filter_name_contains_chana()
    {
        IReadOnlyList<SaleVariantChildDto> filtered = VariantFilter.Filter(Sample(), "chana");

        Assert.Equal(2, filtered.Count);
    }
}
