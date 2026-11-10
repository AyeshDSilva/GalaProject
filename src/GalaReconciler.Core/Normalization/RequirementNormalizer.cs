using System.Text.RegularExpressions;

namespace GalaReconciler.Core.Normalization;

/// <summary>
/// Dietary and accessibility requirements. The RSVP exports have a drop-down column and a "Please specify …"
/// column for each; the master list has one free-text cell. Answers are split into parts so each can be looked for
/// in the master list's cell, whatever the order or punctuation.
/// </summary>
public static partial class RequirementNormalizer
{
    /// <summary>
    /// Answers that carry no requirement of their own. "Other" and "Yes" only say the detail is in the
    /// "Please specify" column. Compared by <see cref="Key"/>; <see cref="TextNormalizer"/>'s placeholders
    /// (nil, none, n/a, no, -) are dropped too.
    /// </summary>
    private static readonly HashSet<string> NoRequirement = new(StringComparer.Ordinal)
    {
        "other", "otherpleasespecify", "yes", "nothing", "notapplicable", "norequirements", "nonerequired",
        "nodietaryrequirements", "noaccessibilityrequirements", "nilrequirements",
    };

    /// <summary>
    /// The requirements in one cell, split on commas, semicolons and line breaks, with blanks and
    /// "no requirement" answers left out.
    /// </summary>
    public static IEnumerable<string> Parts(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
        {
            yield break;
        }

        foreach (var piece in Separators().Split(cell))
        {
            var text = TextNormalizer.Clean(piece);
            if (text is not null && !NoRequirement.Contains(Key(text)))
            {
                yield return text;
            }
        }
    }

    /// <summary>Letters and digits only, lower case: "Gluten-free" = "gluten free" = "GLUTEN FREE.".</summary>
    public static string Key(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>Adds <paramref name="parts"/> to <paramref name="target"/>, skipping any it already has.</summary>
    public static void AddDistinct(List<string> target, IEnumerable<string> parts)
    {
        foreach (var part in parts)
        {
            var key = Key(part);
            if (key.Length > 0 && !target.Any(existing => Key(existing) == key))
            {
                target.Add(part);
            }
        }
    }

    /// <summary>True when the master list's text mentions <paramref name="part"/> (ignoring case, spacing and punctuation).</summary>
    public static bool IsCoveredBy(string part, string? processedText) =>
        processedText is not null && Key(processedText).Contains(Key(part), StringComparison.Ordinal);

    // Not "/": it's part of answers such as "N/A" and "w/c access".
    [GeneratedRegex(@"[,;\r\n]+")]
    private static partial Regex Separators();
}
