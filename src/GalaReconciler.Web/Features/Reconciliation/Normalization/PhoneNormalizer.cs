using System.Text.RegularExpressions;

namespace GalaReconciler.Web.Features.Reconciliation.Normalization;

public static partial class PhoneNormalizer
{
    /// <summary>Splits a cell that may hold several numbers and normalizes each one.</summary>
    public static IReadOnlyList<string> NormalizeAll(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
        {
            return [];
        }

        return Separators().Split(cell)
            .Select(Normalize)
            .Where(phone => phone.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Digits only, Australian international prefix converted to a leading 0, and the leading 0 restored
    /// when Excel stored the number as numeric and dropped it.
    /// </summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "";
        }

        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());

        if (digits.StartsWith("0061", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.StartsWith("610", StringComparison.Ordinal) && digits.Length == 12)
        {
            // "+61 (0)4..." written with the trunk zero kept.
            digits = digits[2..];
        }
        else if (digits.StartsWith("61", StringComparison.Ordinal) && digits.Length == 11)
        {
            digits = "0" + digits[2..];
        }

        // Australian numbers are 10 digits starting with 0; a 9-digit number starting with an area
        // code (2, 3, 7, 8) or mobile prefix (4) has lost its leading zero in Excel.
        if (digits.Length == 9 && digits[0] is '2' or '3' or '4' or '7' or '8')
        {
            digits = "0" + digits;
        }

        return digits;
    }

    [GeneratedRegex(@"\r\n|\r|\n|/|,|;|\||\s+or\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Separators();
}
