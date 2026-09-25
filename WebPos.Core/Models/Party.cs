using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class Party : BaseEntity
{
    public Guid Id { get; set; }

    public string PartyType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Address { get; set; } = string.Empty;

    public long CreditLimitPaisa { get; set; }

    public long CurrentBalancePaisa { get; set; }

    /// <summary>Soft delete for sync: clients remove the record from local stores.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<PartyLedger> Ledgers { get; set; } = new List<PartyLedger>();
}
