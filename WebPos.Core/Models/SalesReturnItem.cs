using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class SalesReturnItem : BaseEntity
{
    public Guid Id { get; set; }

    public Guid SalesReturnId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? BatchId { get; set; }

    public decimal Quantity { get; set; }

    public long RefundUnitPricePaisa { get; set; }

    public string ReturnCondition { get; set; } = string.Empty;

    public SalesReturn SalesReturn { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ProductBatch? Batch { get; set; }
}
