using WebPos.Core.Abstractions;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Services;

/// <summary>
/// Holds the tenant for the current Blazor Server circuit when HttpContext JWT/enrollment claims are absent.
/// </summary>
public sealed class CircuitTenantContext : ICircuitTenantContext
{
    public Guid? TenantId { get; set; }

    public bool IsResolved => TenantId is Guid id && id != Guid.Empty;
}

public interface ICircuitTenantContext
{
    Guid? TenantId { get; set; }

    bool IsResolved { get; }
}

