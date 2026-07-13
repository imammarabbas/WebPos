namespace WebPos.Core.Models;

public class DamagedStockLog
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Guid BatchId { get; set; }

    public decimal Quantity { get; set; }

    public string ReasonCode { get; set; } = string.Empty;

    public Guid LoggedBy { get; set; }

    public long WriteOffLossPaisa { get; set; }

    public DateTimeOffset LoggedAt { get; set; }

    public Product Product { get; set; } = null!;

    public ProductBatch Batch { get; set; } = null!;

    public User LoggedByUser { get; set; } = null!;
}
