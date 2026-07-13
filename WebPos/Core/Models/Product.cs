namespace WebPos.Core.Models;

public class Product
{
    public Guid Id { get; set; }

    public Guid? CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public string BaseUnit { get; set; } = string.Empty;

    public int ConversionMultiplier { get; set; }

    public bool ShowOnWebshop { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Category? Category { get; set; }

    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
}
