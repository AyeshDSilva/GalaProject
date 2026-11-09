using System.Globalization;
using ClosedXML.Excel;

namespace GalaReconciler.Web.Features.Reconciliation.Parsing;

/// <summary>Reads any cell as trimmed text without losing precision on numeric cells (e.g. phone numbers).</summary>
public static class CellText
{
    public static string Read(IXLRow row, int? column) => column is null ? "" : Read(row.Cell(column.Value));

    public static string Read(IXLCell cell)
    {
        var value = cell.Value;
        var text = value.Type switch
        {
            XLDataType.Blank => "",
            XLDataType.Text => value.GetText(),
            XLDataType.Number => FormatNumber(value.GetNumber()),
            XLDataType.Boolean => value.GetBoolean() ? "TRUE" : "FALSE",
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => value.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture),
            XLDataType.Error => "",
            _ => cell.GetString(),
        };
        return text.Trim();
    }

    private static string FormatNumber(double number)
    {
        // Whole numbers (phone numbers stored as numeric, day flags like 1) are written without
        // exponent or decimal point so "61491570159" stays intact.
        if (number == Math.Floor(number) && Math.Abs(number) < 1e18)
        {
            return ((decimal)number).ToString("0", CultureInfo.InvariantCulture);
        }

        return number.ToString("R", CultureInfo.InvariantCulture);
    }
}
