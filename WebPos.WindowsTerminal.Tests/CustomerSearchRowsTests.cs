using WebPos.WindowsTerminal.Services;

namespace WebPos.WindowsTerminal.Tests;

public class CustomerSearchRowsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceQuery_IncludesWalkIn(string? query)
    {
        Assert.True(CustomerSearchRows.IncludeWalkIn(query));
    }

    [Theory]
    [InlineData("mess")]
    [InlineData("a")]
    public void NonEmptyQuery_ExcludesWalkIn(string query)
    {
        Assert.False(CustomerSearchRows.IncludeWalkIn(query));
    }

    [Fact]
    public void EmptyQuery_EnterOnZero_SelectsWalkIn()
    {
        var result = CustomerSearchRows.ResolveEnter(
            highlight: 0,
            hitCount: 3,
            includeWalkIn: true);

        Assert.NotNull(result);
        Assert.Equal(CustomerSearchRows.SelectionKind.WalkIn, result.Value.Kind);
    }

    [Fact]
    public void EmptyQuery_EnterOnOne_SelectsFirstHit()
    {
        var result = CustomerSearchRows.ResolveEnter(
            highlight: 1,
            hitCount: 3,
            includeWalkIn: true);

        Assert.NotNull(result);
        Assert.Equal(CustomerSearchRows.SelectionKind.Customer, result.Value.Kind);
        Assert.Equal(0, result.Value.HitIndex);
    }

    [Fact]
    public void Searching_EnterOnZero_SelectsFirstHit()
    {
        var result = CustomerSearchRows.ResolveEnter(
            highlight: 0,
            hitCount: 2,
            includeWalkIn: false);

        Assert.NotNull(result);
        Assert.Equal(CustomerSearchRows.SelectionKind.Customer, result.Value.Kind);
        Assert.Equal(0, result.Value.HitIndex);
    }

    [Fact]
    public void Searching_EnterOnOne_SelectsSecondHit()
    {
        var result = CustomerSearchRows.ResolveEnter(
            highlight: 1,
            hitCount: 2,
            includeWalkIn: false);

        Assert.NotNull(result);
        Assert.Equal(CustomerSearchRows.SelectionKind.Customer, result.Value.Kind);
        Assert.Equal(1, result.Value.HitIndex);
    }

    [Fact]
    public void Searching_NoHits_EnterDoesNothing()
    {
        Assert.Equal(0, CustomerSearchRows.TotalRows(0, includeWalkIn: false));
        Assert.Null(CustomerSearchRows.ResolveEnter(
            highlight: 0,
            hitCount: 0,
            includeWalkIn: false));
    }

    [Fact]
    public void EmptyQuery_TotalRowsIncludesWalkIn()
    {
        Assert.Equal(4, CustomerSearchRows.TotalRows(3, includeWalkIn: true));
    }
}
