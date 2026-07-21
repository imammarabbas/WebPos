namespace WebPos.Core.Interfaces;

public interface ITenantService
{
    Guid TenantId { get; }

    bool IsResolved { get; }
}
