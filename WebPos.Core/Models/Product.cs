using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class Product : BaseEntity
{
    public Guid Id { get; set; }

    public Guid? CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    /// <summary>Numeric PLU for cashier keypad / loose-item lookup (e.g. 2001).</summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>Loose / no-barcode items sold by short code tap or keypad.</summary>
    public bool IsLoose { get; set; }

    public string Brand { get; set; } = string.Empty;

    /// <summary>Unit used when selling / counting stock (e.g. g, ml, PCS).</summary>
    public string BaseUnit { get; set; } = string.Empty;

    /// <summary>Unit used when receiving purchases (e.g. kg). Empty = same as <see cref="BaseUnit"/>.</summary>
    public string PurchaseUnit { get; set; } = string.Empty;

    /// <summary>
    /// How many <see cref="BaseUnit"/> stock units equal 1 <see cref="PurchaseUnit"/>
    /// (e.g. 1000 when buying kg and selling g).
    /// </summary>
    public int ConversionMultiplier { get; set; }

    public bool ShowOnWebshop { get; set; }

    /// <summary>Alert when summed batch qty falls below this threshold.</summary>
    public decimal MinStockQty { get; set; } = 10m;

    /// <summary>Soft delete for sync: clients remove the record from local stores.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Category? Category { get; set; }

    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
}
