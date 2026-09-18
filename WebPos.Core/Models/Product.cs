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

    /// <summary>When true, product appears on the POS Quick Items strip.</summary>
    public bool ShowOnPosQuick { get; set; }

    /// <summary>Lower values sort first on the POS Quick Items strip.</summary>
    public int PosQuickSort { get; set; }

    /// <summary>Alert when <see cref="StockQty"/> falls below this threshold.</summary>
    public decimal MinStockQty { get; set; } = 10m;

    /// <summary>Inventory pool on the master product (aliases keep 0).</summary>
    public decimal StockQty { get; set; }

    /// <summary>Rolling weighted-average unit cost in paisa (parent base units).</summary>
    public long CostPricePaisa { get; set; }

    /// <summary>Catalog sell price in paisa (masters and aliases).</summary>
    public long RetailPricePaisa { get; set; }

    /// <summary>
    /// Bulk warehouse SKU: holds the shared stock pool and may be selected as a
    /// child-variant parent. Independent retail items stay <c>false</c>.
    /// </summary>
    public bool IsBulk { get; set; }

    /// <summary>
    /// Default retail margin percent for child packs of a bulk parent (e.g. 20 = 20%).
    /// </summary>
    public decimal DefaultMarginPercent { get; set; } = 20m;

    /// <summary>When set, this SKU is a virtual alias of the parent master product.</summary>
    public Guid? ParentProductId { get; set; }

    /// <summary>
    /// Parent base units consumed per 1 unit of this alias sold
    /// (e.g. 50 when parent is g and this is a 50g pack).
    /// </summary>
    public decimal? DeductionMultiplier { get; set; }

    /// <summary>Soft delete for sync: clients remove the record from local stores.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Category? Category { get; set; }

    public Product? ParentProduct { get; set; }

    public ICollection<Product> ChildAliases { get; set; } = new List<Product>();

    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
}
