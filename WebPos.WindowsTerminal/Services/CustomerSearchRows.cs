namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Customer search list row model: Walk-In only when the query is empty.
/// </summary>
public static class CustomerSearchRows
{
    public enum SelectionKind
    {
        WalkIn,
        Customer
    }

    public static bool IncludeWalkIn(string? query) =>
        string.IsNullOrWhiteSpace(query);

    public static int TotalRows(int hitCount, bool includeWalkIn)
    {
        int hits = Math.Max(0, hitCount);
        return hits + (includeWalkIn ? 1 : 0);
    }

    /// <summary>
    /// Maps highlight index to Walk-In or a customer hit index.
    /// Returns null when there are no rows or the index is out of range.
    /// </summary>
    public static (SelectionKind Kind, int HitIndex)? ResolveEnter(
        int highlight,
        int hitCount,
        bool includeWalkIn)
    {
        int total = TotalRows(hitCount, includeWalkIn);
        if (total <= 0)
        {
            return null;
        }

        int idx = Math.Clamp(highlight, 0, total - 1);

        if (includeWalkIn)
        {
            if (idx == 0)
            {
                return (SelectionKind.WalkIn, -1);
            }

            return (SelectionKind.Customer, idx - 1);
        }

        return (SelectionKind.Customer, idx);
    }
}
