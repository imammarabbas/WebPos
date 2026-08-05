using Common.Models;

namespace WebPos.WindowsTerminal.Services;

public sealed class HeldCartSnapshot
{
    public required IReadOnlyList<CartLine> Lines { get; init; }

    public PartyDto? Customer { get; init; }

    public bool UseWalkIn { get; init; } = true;

    public long DiscountAmountPaisa { get; init; }
}

public sealed class CartHoldService
{
    public HeldCartSnapshot? Held { get; private set; }

    public bool HasHeldCart => Held is not null && Held.Lines.Count > 0;

    public void Hold(HeldCartSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Lines.Count == 0)
        {
            return;
        }

        Held = snapshot;
    }

    public HeldCartSnapshot? Resume()
    {
        HeldCartSnapshot? snapshot = Held;
        Held = null;
        return snapshot;
    }

    public void Clear() => Held = null;
}
