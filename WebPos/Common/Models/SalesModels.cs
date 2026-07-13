namespace Common.Models;

/// <summary>
/// Shared API/test model surface. Sale DTOs are defined in <c>WebPos.Core.Abstractions</c>
/// and imported by consumers as <see cref="WebPos.Core.Abstractions.CompleteSaleRequest"/>.
/// </summary>
public static class SalesModels
{
    // Intentionally empty marker — keep CompleteSaleRequest / CompleteSaleResult in Core.Abstractions
    // as the single source of truth used by SalesController and integration tests.
}
