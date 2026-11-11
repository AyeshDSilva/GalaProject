using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GalaReconciler.Core.Normalization;

public static partial class NameNormalizer
{
    private static readonly HashSet<string> Titles = new(StringComparer.Ordinal)
    {
        "mr", "mrs", "ms", "miss", "mx", "dr", "prof", "professor", "sir", "dame", "lady", "lord", "the", "hon", "rev", "cr",
    };

    /// <summary>Match key for a person: normalized "first last".</summary>
    public static string Key(string? firstName, string? lastName) => Normalize($"{firstName} {lastName}");

    /// <summary>
    /// Lowercase, strip diacritics, drop apostrophes, treat hyphens and other punctuation as spaces,
    /// collapse whitespace and strip leading titles ("Mr Mr Oscar Reid" → "oscar reid").
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        var text = RemoveDiacritics(name).ToLowerInvariant();
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is '\'' or '’' or '‘' or '`' or '´')
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        var tokens = builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', tokens.Skip(CountLeadingTitles(tokens)));
    }

    /// <summary>Removes leading titles but keeps the original casing, for display ("Mr Mr Oscar Reid" → "Oscar Reid").</summary>
    public static string StripTitles(string? name)
    {
        var tokens = CleanDisplay(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', tokens.Skip(CountLeadingTitles(tokens)));
    }

    /// <summary>Trims and collapses whitespace (including newlines) without changing anything else.</summary>
    public static string CleanDisplay(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "" : Whitespace().Replace(text.Trim(), " ");

    private static int CountLeadingTitles(string[] tokens)
    {
        // Always keep at least one token so a name is never stripped to nothing.
        var count = 0;
        while (count < tokens.Length - 1 && Titles.Contains(tokens[count].TrimEnd('.').ToLowerInvariant()))
        {
            count++;
        }

        return count;
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
