using FluentAssertions;
using WebPos.Core;

namespace WebPos.IntegrationTests.Services;

public sealed class PackSizeParserTests
{
    [Theory]
    [InlineData("50g", "g", 50)]
    [InlineData("50 g", "g", 50)]
    [InlineData("1kg", "g", 1000)]
    [InlineData("1KG", "g", 1000)]
    [InlineData("50g", "kg", 0.05)]
    [InlineData("1kg", "kg", 1)]
    [InlineData("500ml", "ml", 500)]
    [InlineData("500ml", "L", 0.5)]
    [InlineData("1L", "ml", 1000)]
    [InlineData("50", "g", 50)]
    public void TryParse_ConvertsPackIntoParentUnits(string pack, string parentUnit, decimal expected)
    {
        PackSizeParser.TryParse(pack, parentUnit, out decimal multiplier, out string? error)
            .Should().BeTrue(error);
        multiplier.Should().Be(expected);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "g")]
    [InlineData("", "g")]
    [InlineData("abc", "g")]
    [InlineData("50g", "ml")]
    [InlineData("0g", "g")]
    public void TryParse_RejectsInvalidInput(string? pack, string parentUnit)
    {
        PackSizeParser.TryParse(pack, parentUnit, out decimal multiplier, out string? error)
            .Should().BeFalse();
        multiplier.Should().Be(0);
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(50, "g", "50g")]
    [InlineData(1000, "g", "1kg")]
    [InlineData(0.05, "kg", "50g")]
    [InlineData(1, "kg", "1kg")]
    [InlineData(500, "ml", "500ml")]
    [InlineData(1, "L", "1L")]
    public void Format_UsesFriendlyUnits(decimal multiplier, string parentUnit, string expected)
    {
        PackSizeParser.Format(multiplier, parentUnit).Should().Be(expected);
    }
}
