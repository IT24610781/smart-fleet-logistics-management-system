using FleetVehicleService.Services;

namespace FleetVehicleService.Tests;

/// <summary>
/// Capacity is a positive number with an optional unit ("12", "500 kg",
/// "8 persons"). Pure unit tests - no database or web server needed.
/// </summary>
public class CapacityParserTests
{
    [Theory]
    [InlineData("12", 12, "", "12")]
    [InlineData("1 unit", 1, "unit", "1 unit")]
    [InlineData("1 kg", 1, "kg", "1 kg")]
    [InlineData("1 person", 1, "person", "1 person")]
    [InlineData("8 persons", 8, "persons", "8 persons")]
    [InlineData("500kg", 500, "kg", "500 kg")]
    [InlineData("2.5 tons", 2.5, "tons", "2.5 tons")]
    [InlineData("  20   litres ", 20, "litres", "20 litres")]
    [InlineData("007 kg", 7, "kg", "7 kg")]
    [InlineData("10 kg/day", 10, "kg/day", "10 kg/day")]
    public void TryParse_ValidInput_ReturnsAmountUnitAndNormalisedText(
        string input, double expectedAmount, string expectedUnit, string expectedNormalized)
    {
        var ok = CapacityParser.TryParse(input, out var amount, out var unit, out var normalized);

        Assert.True(ok);
        Assert.Equal((decimal)expectedAmount, amount);
        Assert.Equal(expectedUnit, unit);
        Assert.Equal(expectedNormalized, normalized);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("0 kg")]
    [InlineData("0.00 kg")]
    [InlineData("-3 kg")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("kg")]
    [InlineData("5 @#")]
    [InlineData("1 2 kg")]
    [InlineData("1,000 kg")]
    [InlineData("1.234 kg")]
    [InlineData("1000000000000 kg")]
    [InlineData("5 abcdefghijklmnopqrstuvwxyz")]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        Assert.False(CapacityParser.TryParse(input, out _, out _, out _));
    }

    [Fact]
    public void TryParse_LongestPossibleValue_StillFitsTheVarchar50Column()
    {
        // 9 digits + 2 decimals + space + 20-char unit = 33 characters (< 50).
        var ok = CapacityParser.TryParse("999999999.99 " + "a" + new string('b', 19), out _, out _, out var normalized);

        Assert.True(ok);
        Assert.True(normalized.Length <= 50);
    }
}
