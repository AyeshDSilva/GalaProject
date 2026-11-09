using System.Text.RegularExpressions;

namespace GalaReconciler.Core.Normalization;

public static partial class EmailNormalizer
{
    /// <summary>Splits a cell that may hold several addresses, lowercases them and drops placeholders.</summary>
    public static IReadOnlyList<string> NormalizeAll(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
        {
            return [];
        }

        return Separators().Split(cell)
            .Select(Normalize)
            .Where(email => email.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static string Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "";
        }

        var value = email.Trim().Trim('<', '>', '"', '\'', '(', ')').ToLowerInvariant();
        if (value.StartsWith("mailto:", StringComparison.Ordinal))
        {
            value = value["mailto:".Length..];
        }

        // Placeholders such as "nil" or "-", or anything with no letters or digits at all.
        if (TextNormalizer.IsPlaceholder(value))
        {
            return "";
        }

        return value;
    }

    [GeneratedRegex(@"[\s,;]+")]
    private static partial Regex Separators();
}
