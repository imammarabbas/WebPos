using System.Globalization;
using System.Text.RegularExpressions;

namespace WebPos.Core;

/// <summary>
/// Parses sellable pack sizes such as "50g" or "1kg" into a parent-stock
/// <c>DeductionMultiplier</c> (parent base units consumed per 1 pack sold).
/// </summary>
public static partial class PackSizeParser
{
    private enum MeasureFamily
    {
        Mass,
        Volume,
        Count
    }

    [GeneratedRegex(@"^\s*(?<qty>\d+(?:[.,]\d+)?)\s*(?<unit>[A-Za-z]+)?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PackSizePattern();

    public static bool TryParse(
        string? packSize,
        string? parentBaseUnit,
        out decimal deductionMultiplier,
        out string? error)
    {
        deductionMultiplier = 0m;
        error = null;

        if (string.IsNullOrWhiteSpace(packSize))
        {
            error = "Enter a pack size such as 50g or 1kg.";
            return false;
        }

        Match match = PackSizePattern().Match(packSize);
        if (!match.Success)
        {
            error = "Pack size must look like 50g or 1kg.";
            return false;
        }

        string qtyRaw = match.Groups["qty"].Value.Replace(',', '.');
        if (!decimal.TryParse(qtyRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal qty)
            || qty <= 0)
        {
            error = "Pack size quantity must be greater than zero.";
            return false;
        }

        string packUnitRaw = match.Groups["unit"].Value;
        string parentUnit = string.IsNullOrWhiteSpace(parentBaseUnit) ? "PCS" : parentBaseUnit.Trim();

        if (string.IsNullOrWhiteSpace(packUnitRaw))
        {
            deductionMultiplier = decimal.Round(qty, 3, MidpointRounding.AwayFromZero);
            return deductionMultiplier > 0;
        }

        if (!TryNormalizeUnit(packUnitRaw, out MeasureFamily packFamily, out decimal packToCanonical))
        {
            error = $"Unknown pack unit '{packUnitRaw}'.";
            return false;
        }

        if (!TryNormalizeUnit(parentUnit, out MeasureFamily parentFamily, out decimal parentToCanonical))
        {
            error = $"Cannot convert pack size into parent unit '{parentUnit}'.";
            return false;
        }

        if (packFamily != parentFamily)
        {
            error = $"Pack unit '{packUnitRaw}' does not match parent unit '{parentUnit}'.";
            return false;
        }

        if (parentToCanonical == 0)
        {
            error = $"Cannot convert pack size into parent unit '{parentUnit}'.";
            return false;
        }

        decimal packCanonical = qty * packToCanonical;
        deductionMultiplier = decimal.Round(
            packCanonical / parentToCanonical,
            3,
            MidpointRounding.AwayFromZero);
        if (deductionMultiplier <= 0)
        {
            error = "Pack size is too small for the parent unit.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Formats a stored multiplier back into a typed pack size for the edit form.
    /// </summary>
    public static string Format(decimal deductionMultiplier, string? parentBaseUnit)
    {
        if (deductionMultiplier <= 0)
        {
            return string.Empty;
        }

        string parentUnit = string.IsNullOrWhiteSpace(parentBaseUnit) ? "g" : parentBaseUnit.Trim();
        if (!TryNormalizeUnit(parentUnit, out MeasureFamily family, out decimal parentToCanonical))
        {
            return $"{TrimNumber(deductionMultiplier)}{parentUnit}";
        }

        decimal canonical = deductionMultiplier * parentToCanonical;
        (string unit, decimal factor) = PreferDisplayUnit(family, canonical);
        return $"{TrimNumber(canonical / factor)}{unit}";
    }

    private static (string Unit, decimal CanonicalPerUnit) PreferDisplayUnit(
        MeasureFamily family,
        decimal canonical)
    {
        if (family == MeasureFamily.Mass)
        {
            if (canonical >= 1000m && canonical % 1000m == 0)
            {
                return ("kg", 1000m);
            }

            if (canonical < 1m)
            {
                return ("mg", 0.001m);
            }

            return ("g", 1m);
        }

        if (family == MeasureFamily.Volume)
        {
            if (canonical >= 1000m && canonical % 1000m == 0)
            {
                return ("L", 1000m);
            }

            return ("ml", 1m);
        }

        return PreferCountDisplay(canonical);
    }

    private static (string Unit, decimal CanonicalPerUnit) PreferCountDisplay(decimal canonical)
    {
        if (canonical >= 12m && canonical % 12m == 0)
        {
            return ("dozen", 12m);
        }

        return ("PCS", 1m);
    }

    private static bool TryNormalizeUnit(
        string raw,
        out MeasureFamily family,
        out decimal canonicalPerUnit)
    {
        family = MeasureFamily.Count;
        canonicalPerUnit = 1m;
        string key = raw.Trim().ToLowerInvariant().TrimEnd('.');
        switch (key)
        {
            case "mg":
            case "milligram":
            case "milligrams":
                family = MeasureFamily.Mass;
                canonicalPerUnit = 0.001m;
                return true;
            case "g":
            case "gram":
            case "grams":
                family = MeasureFamily.Mass;
                canonicalPerUnit = 1m;
                return true;
            case "kg":
            case "kilo":
            case "kilos":
            case "kilogram":
            case "kilograms":
                family = MeasureFamily.Mass;
                canonicalPerUnit = 1000m;
                return true;
            case "ml":
            case "millilitre":
            case "milliliter":
            case "millilitres":
            case "milliliters":
                family = MeasureFamily.Volume;
                canonicalPerUnit = 1m;
                return true;
            case "l":
            case "lt":
            case "liter":
            case "litre":
            case "liters":
            case "litres":
                family = MeasureFamily.Volume;
                canonicalPerUnit = 1000m;
                return true;
            case "pc":
            case "pcs":
            case "piece":
            case "pieces":
            case "ea":
            case "each":
                family = MeasureFamily.Count;
                canonicalPerUnit = 1m;
                return true;
            case "doz":
            case "dozen":
            case "dozens":
                family = MeasureFamily.Count;
                canonicalPerUnit = 12m;
                return true;
            default:
                return false;
        }
    }

    private static string TrimNumber(decimal value)
    {
        string formatted = value.ToString("0.###", CultureInfo.InvariantCulture);
        return formatted;
    }
}
