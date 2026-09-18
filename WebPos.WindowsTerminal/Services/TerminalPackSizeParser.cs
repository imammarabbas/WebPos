using System.Globalization;
using System.Text.RegularExpressions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Client-side pack size → deduction multiplier (mirrors Core PackSizeParser essentials).</summary>
public static partial class TerminalPackSizeParser
{
    [GeneratedRegex(@"^\s*(?<qty>\d+(?:[.,]\d+)?)\s*(?<unit>[A-Za-z]+)?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex PackSizePattern();

    public static bool TryParse(string? packSize, string? parentBaseUnit, out decimal deductionMultiplier, out string? error)
    {
        deductionMultiplier = 0m;
        error = null;
        if (string.IsNullOrWhiteSpace(packSize))
        {
            error = "Enter a pack size such as 50g, 1kg, 12, or 1dozen.";
            return false;
        }

        Match match = PackSizePattern().Match(packSize);
        if (!match.Success)
        {
            error = "Pack size must look like 50g, 1kg, or 1dozen.";
            return false;
        }

        string qtyRaw = match.Groups["qty"].Value.Replace(',', '.');
        if (!decimal.TryParse(qtyRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal qty) || qty <= 0)
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

        if (!TryFactor(packUnitRaw, out decimal packFactor) || !TryFactor(parentUnit, out decimal parentFactor))
        {
            error = $"Cannot convert '{packUnitRaw}' into parent unit '{parentUnit}'.";
            return false;
        }

        if (!SameFamily(packUnitRaw, parentUnit))
        {
            error = $"Pack unit '{packUnitRaw}' does not match parent unit '{parentUnit}'.";
            return false;
        }

        deductionMultiplier = decimal.Round(qty * packFactor / parentFactor, 3, MidpointRounding.AwayFromZero);
        if (deductionMultiplier <= 0)
        {
            error = "Pack size is too small for the parent unit.";
            return false;
        }

        return true;
    }

    public static string Format(decimal deductionMultiplier, string? parentBaseUnit)
    {
        if (deductionMultiplier <= 0)
        {
            return string.Empty;
        }

        string parent = string.IsNullOrWhiteSpace(parentBaseUnit) ? "PCS" : parentBaseUnit.Trim();
        if (TryFactor(parent, out decimal parentFactor) && IsMass(parent))
        {
            decimal grams = deductionMultiplier * parentFactor;
            if (grams >= 1000m && grams % 1000m == 0)
            {
                return $"{(grams / 1000m).ToString("0.###", CultureInfo.InvariantCulture)}kg";
            }

            return $"{grams.ToString("0.###", CultureInfo.InvariantCulture)}g";
        }

        if (deductionMultiplier >= 12m && deductionMultiplier % 12m == 0 && IsCount(parent))
        {
            return $"{(deductionMultiplier / 12m).ToString("0.###", CultureInfo.InvariantCulture)}dozen";
        }

        return $"{deductionMultiplier.ToString("0.###", CultureInfo.InvariantCulture)}";
    }

    private static bool SameFamily(string a, string b) =>
        (IsMass(a) && IsMass(b)) || (IsCount(a) && IsCount(b)) || (IsVolume(a) && IsVolume(b));

    private static bool IsMass(string u)
    {
        string k = u.Trim().ToLowerInvariant();
        return k is "mg" or "g" or "kg" or "gram" or "grams" or "kilogram" or "kilograms";
    }

    private static bool IsVolume(string u)
    {
        string k = u.Trim().ToLowerInvariant();
        return k is "ml" or "l" or "lt" or "liter" or "litre" or "liters" or "litres";
    }

    private static bool IsCount(string u)
    {
        string k = u.Trim().ToLowerInvariant();
        return k is "pc" or "pcs" or "piece" or "pieces" or "ea" or "each" or "doz" or "dozen" or "dozens";
    }

    private static bool TryFactor(string raw, out decimal factor)
    {
        factor = 1m;
        switch (raw.Trim().ToLowerInvariant().TrimEnd('.'))
        {
            case "mg":
                factor = 0.001m;
                return true;
            case "g":
            case "gram":
            case "grams":
                factor = 1m;
                return true;
            case "kg":
            case "kilogram":
            case "kilograms":
                factor = 1000m;
                return true;
            case "ml":
                factor = 1m;
                return true;
            case "l":
            case "lt":
            case "liter":
            case "litre":
                factor = 1000m;
                return true;
            case "pc":
            case "pcs":
            case "piece":
            case "pieces":
            case "ea":
            case "each":
                factor = 1m;
                return true;
            case "doz":
            case "dozen":
            case "dozens":
                factor = 12m;
                return true;
            default:
                return false;
        }
    }
}
