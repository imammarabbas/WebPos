namespace WebPos.Core.Models;

public class ProductionConsumptionItem
{
    public Guid Id { get; set; }

    public Guid ProductionLogId { get; set; }

    public Guid ProductId { get; set; }

    public decimal QuantityConsumed { get; set; }

    public ProductionLog ProductionLog { get; set; } = null!;

    public Product Product { get; set; } = null!;
}
