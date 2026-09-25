using Common.Models;

namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Client-side ranked filter for the Select Variant modal (Omarchy-style type-to-filter).
/// </summary>
public static class VariantFilter
{
    /// <summary>
    /// Rank: 0 exact, 1 starts-with, 2 token starts-with, 3 contains, 4 fuzzy subsequence; -1 no match.
    /// </summary>
    public static int RankMatch(SaleVariantChildDto child, string query)
    {
        ArgumentNullException.ThrowIfNull(child);

        string q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
        {
            return 0;
        }

        int best = -1;
        foreach (string raw in MatchFields(child))
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            int rank = RankField(raw.Trim(), q);
            if (rank < 0)
            {
                continue;
            }

            if (best < 0 || rank < best)
            {
                best = rank;
            }

            if (best == 0)
            {
                return 0;
            }
        }

        return best;
    }

    public static IReadOnlyList<SaleVariantChildDto> Filter(
        IReadOnlyList<SaleVariantChildDto> source,
        string query)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Count == 0)
        {
            return source;
        }

        string q = (query ?? string.Empty).Trim();
        if (q.Length == 0)
        {
            return source;
        }

        return source
            .Select(c => (Child: c, Rank: RankMatch(c, q)))
            .Where(x => x.Rank >= 0)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Child.PackingSize, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Child)
            .ToList();
    }

    private static int RankField(string field, string q)
    {
        if (field.Equals(q, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (field.StartsWith(q, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        foreach (string token in Tokenize(field))
        {
            if (token.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
        }

        if (field.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (IsFuzzySubsequence(field, q))
        {
            return 4;
        }

        return -1;
    }

    private static IEnumerable<string> Tokenize(string field)
    {
        int start = -1;
        for (int i = 0; i <= field.Length; i++)
        {
            bool end = i == field.Length;
            char c = end ? '\0' : field[i];
            bool sep = end || char.IsWhiteSpace(c) || c is '-' or '_' or '/' or '.';
            if (!sep)
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0)
            {
                yield return field[start..i];
                start = -1;
            }
        }
    }

    private static bool IsFuzzySubsequence(string field, string q)
    {
        int fi = 0;
        for (int qi = 0; qi < q.Length; qi++)
        {
            char needle = char.ToLowerInvariant(q[qi]);
            bool found = false;
            while (fi < field.Length)
            {
                if (char.ToLowerInvariant(field[fi++]) == needle)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> MatchFields(SaleVariantChildDto child)
    {
        yield return child.PackingSize ?? string.Empty;
        yield return child.ShortCode ?? string.Empty;
        yield return child.Sku ?? string.Empty;
        yield return child.Barcode ?? string.Empty;
        yield return child.Name ?? string.Empty;
    }
}
