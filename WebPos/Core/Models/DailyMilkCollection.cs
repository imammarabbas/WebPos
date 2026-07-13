namespace WebPos.Core.Models;

public class DailyMilkCollection
{
    public Guid Id { get; set; }

    public Guid SupplierId { get; set; }

    public string MilkType { get; set; } = string.Empty;

    public decimal LitersReceived { get; set; }

    public long RatePerLiterPaisa { get; set; }

    public long TotalCreditPaisa { get; set; }

    public DateTimeOffset CollectionTime { get; set; }

    public Party Supplier { get; set; } = null!;
}
