namespace WebPos.Core.Models;

public class ProductionYieldItem
{
    public Guid Id { get; set; }

    public Guid ProductionLogId { get; set; }

    public Guid ProductId { get; set; }

    public decimal QuantityProduced { get; set; }

    public long CalculatedCostPricePaisa { get; set; }

    public Guid? TargetBatchId { get; set; }

    public ProductionLog ProductionLog { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ProductBatch? TargetBatch { get; set; }
}
