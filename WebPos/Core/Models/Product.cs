using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class Product : BaseEntity
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

    /// <summary>Soft delete for sync: clients remove the record from local stores.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Category? Category { get; set; }

    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
}
