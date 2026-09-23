using System.Globalization;
using System.Text.RegularExpressions;

namespace TripDispatchService.Services;

public static class CapacityParser
{
    // Amount: up to 9 digits, optional 1-2 decimal places.
    // Unit:   starts with a letter; letters, digits, spaces, "." "/" "-";
    // at most 20 characters. Together they always fit VARCHAR(50).
    private static readonly Regex Pattern = new(
        @"^(?<amount>[0-9]{1,9}(?:\.[0-9]{1,2})?)\s*(?<unit>\p{L}[\p{L}\p{N} ./-]{0,19})?\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(
        string? input,
        out decimal amount,
        out string unit,
        out string normalized)
    {
        amount = 0;
        unit = string.Empty;
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var match = Pattern.Match(input.Trim());

        if (!match.Success)
            return false;

        if (!decimal.TryParse(
                match.Groups["amount"].Value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsedAmount) || parsedAmount <= 0)
        {
            return false;
        }

        amount = parsedAmount;

        // Collapse repeated spaces so "20   litres" and "20 litres" match.
        unit = Regex.Replace(match.Groups["unit"].Value.Trim(), @"\s+", " ");

        var amountText = amount.ToString(CultureInfo.InvariantCulture);

        normalized = unit.Length == 0 ? amountText : $"{amountText} {unit}";

        return true;
    }
}