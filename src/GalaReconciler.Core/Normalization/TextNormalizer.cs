using System.Text.RegularExpressions;

namespace GalaReconciler.Core.Normalization;

/// <summary>Free-text fields such as title, post nominal and position title.</summary>
public static partial class TextNormalizer
{
    private static readonly HashSet<string> Placeholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "nil", "none", "n/a", "na", "tba", "tbc", "unknown", "no", "0",
    };

    /// <summary>"nil", "-", "n/a" and similar mean "no value".</summary>
    public static bool IsPlaceholder(string value) =>
        Placeholders.Contains(value.Trim()) || !value.Any(char.IsLetterOrDigit);

    /// <summary>Trimmed text with whitespace collapsed, or null when blank or a placeholder.</summary>
    public static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = Whitespace().Replace(value.Trim(), " ");
        return IsPlaceholder(text) ? null : text;
    }

    /// <summary>For comparing: case, full stops and spacing don't count ("Mr." = "mr", "O.A.M." = "OAM").</summary>
    public static string ComparisonKey(string value) =>
        Whitespace().Replace(value.Replace(".", " "), " ").Trim().ToLowerInvariant().Replace(" ", "");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
