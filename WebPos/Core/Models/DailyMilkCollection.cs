using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class DailyMilkCollection : BaseEntity
{
    public Guid Id { get; set; }

    public Guid SupplierId { get; set; }

    public string MilkType { get; set; } = string.Empty;

    public decimal LitersReceived { get; set; }

    /// <summary>Optional fat percentage for dairy quality tracking.</summary>
    public decimal? FatPercent { get; set; }

    /// <summary>Optional solids-not-fat percentage for dairy quality tracking.</summary>
    public decimal? SnfPercent { get; set; }

    public long RatePerLiterPaisa { get; set; }

    public long TotalCreditPaisa { get; set; }

    public DateTimeOffset CollectionTime { get; set; }

    public Party Supplier { get; set; } = null!;
}
