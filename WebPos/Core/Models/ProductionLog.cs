namespace WebPos.Core.Models;

public class ProductionLog
{
    public Guid Id { get; set; }

    public string BatchReference { get; set; } = string.Empty;

    public Guid OperatorId { get; set; }

    public long AdditionalOverheadPaisa { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User Operator { get; set; } = null!;

    public ICollection<ProductionConsumptionItem> ConsumptionItems { get; set; } = new List<ProductionConsumptionItem>();

    public ICollection<ProductionYieldItem> YieldItems { get; set; } = new List<ProductionYieldItem>();
}
