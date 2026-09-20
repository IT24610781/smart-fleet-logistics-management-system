using System.Globalization;
using System.Text.RegularExpressions;

namespace FleetVehicleService.Services;

// ============================================================
// Vehicle capacity: a positive number with an OPTIONAL unit.
//
//   "12"          -> 12
//   "500 kg"      -> 500 kg
//   "500kg"       -> 500 kg      (normalised to "<number> <unit>")
//   "8 persons"   -> 8 persons
//   "2.5 tons"    -> 2.5 tons
//
// Capacity is stored as text (capacity VARCHAR(50)), so the unit can be
// anything a dispatcher understands (kg, persons, units, litres...).
// This parser is the ONE place that decides what is valid, and it also
// gives trip matching (US-10) a number to compare instead of raw text.
// ============================================================

public static class CapacityParser
{
    // Amount: up to 9 digits, optional 1-2 decimal places.
    // Unit:   starts with a letter; letters, digits, spaces, "." "/" "-";
    //         at most 20 characters. Together they always fit VARCHAR(50).
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
