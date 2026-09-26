using WebPos.WindowsTerminal.Services;

namespace WebPos.WindowsTerminal.Tests;

/// <summary>
/// Acceptance scenarios for F4 customer search keyboard behavior
/// (same IncludeWalkIn / highlight / Enter rules as CustomerSearchModal).
/// </summary>
public class CustomerSearchAcceptanceTests
{
    [Fact]
    public void Acceptance1_EmptyQuery_EnterSelectsWalkIn()
    {
        // Open F4 with empty query: Walk-In is row 0 and highlighted.
        string query = string.Empty;
        int highlight = 0;
        int hitCount = 3; // other customers listed below Walk-In

        bool includeWalkIn = CustomerSearchRows.IncludeWalkIn(query);
        Assert.True(includeWalkIn);
        Assert.Equal(0, highlight);

        var selection = CustomerSearchRows.ResolveEnter(highlight, hitCount, includeWalkIn);
        Assert.NotNull(selection);
        Assert.Equal(CustomerSearchRows.SelectionKind.WalkIn, selection.Value.Kind);
    }

    [Fact]
    public void Acceptance2_TypeMess_WalkInGone_EnterSelectsFirstMatch()
    {
        // Type "mess" without clicking: Walk-In disappears; first match is row 0 / highlighted.
        string query = "mess";
        int highlight = 0; // OnQueryChanged resets highlight to top row
        int hitCount = 1; // e.g. Messum Abbas only

        bool includeWalkIn = CustomerSearchRows.IncludeWalkIn(query);
        Assert.False(includeWalkIn);
        Assert.Equal(1, CustomerSearchRows.TotalRows(hitCount, includeWalkIn));

        var selection = CustomerSearchRows.ResolveEnter(highlight, hitCount, includeWalkIn);
        Assert.NotNull(selection);
        Assert.Equal(CustomerSearchRows.SelectionKind.Customer, selection.Value.Kind);
        Assert.Equal(0, selection.Value.HitIndex);
    }

    [Fact]
    public void Acceptance3_ArrowDown_EnterSelectsSecondMatch()
    {
        string query = "abbas";
        int highlight = 0;
        int hitCount = 2;

        bool includeWalkIn = CustomerSearchRows.IncludeWalkIn(query);
        Assert.False(includeWalkIn);

        int total = CustomerSearchRows.TotalRows(hitCount, includeWalkIn);
        highlight = Math.Min(highlight + 1, total - 1); // ArrowDown

        var selection = CustomerSearchRows.ResolveEnter(highlight, hitCount, includeWalkIn);
        Assert.NotNull(selection);
        Assert.Equal(CustomerSearchRows.SelectionKind.Customer, selection.Value.Kind);
        Assert.Equal(1, selection.Value.HitIndex);
    }

    [Fact]
    public void Acceptance4_NoMatches_WalkInHidden_EnterNoOps()
    {
        string query = "zzzz-no-match";
        bool includeWalkIn = CustomerSearchRows.IncludeWalkIn(query);
        int hitCount = 0;

        Assert.False(includeWalkIn);
        Assert.Equal(0, CustomerSearchRows.TotalRows(hitCount, includeWalkIn));
        Assert.Null(CustomerSearchRows.ResolveEnter(0, hitCount, includeWalkIn));
    }

    [Fact]
    public void Acceptance5_BackspaceToEmpty_WalkInReturnsAsRowZero()
    {
        Assert.False(CustomerSearchRows.IncludeWalkIn("mess"));

        string query = string.Empty; // fully cleared
        int highlight = 0; // OnQueryChanged

        bool includeWalkIn = CustomerSearchRows.IncludeWalkIn(query);
        Assert.True(includeWalkIn);

        var selection = CustomerSearchRows.ResolveEnter(highlight, hitCount: 3, includeWalkIn);
        Assert.NotNull(selection);
        Assert.Equal(CustomerSearchRows.SelectionKind.WalkIn, selection.Value.Kind);
    }
}
