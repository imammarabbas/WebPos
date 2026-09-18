using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class StockCount : BaseEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset CountedAt { get; set; }

    public string? Note { get; set; }

    public Guid? CountedByUserId { get; set; }

    public ICollection<StockCountLine> Lines { get; set; } = new List<StockCountLine>();
}

public class StockCountLine : BaseEntity
{
    public Guid Id { get; set; }

    public Guid StockCountId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? BatchId { get; set; }

    public decimal CountedQty { get; set; }

    public StockCount StockCount { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ProductBatch? Batch { get; set; }
}
