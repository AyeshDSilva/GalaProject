using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace GalaReconciler.Core.Parsing;

/// <summary>
/// Finds columns by their header text (never by letter). Headers can repeat, so lookups can ask for
/// the first match after a given column.
/// </summary>
public sealed partial class HeaderMap
{
    private readonly List<(string Header, int Column)> _headers = [];
    private readonly StringComparison _comparison;

    /// <param name="comparison">
    /// Use <see cref="StringComparison.Ordinal"/> when headers differ only by case
    /// (the Source file's "First name" vs "First Name").
    /// </param>
    public HeaderMap(IXLWorksheet sheet, StringComparison comparison, int headerRow = 1)
    {
        _comparison = comparison;
        var row = sheet.Row(headerRow);
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            var header = Whitespace().Replace(CellText.Read(row.Cell(column)), " ");
            if (header.Length > 0)
            {
                _headers.Add((header, column));
            }
        }
    }

    public IReadOnlyList<string> Headers => _headers.Select(h => h.Header).ToList();

    /// <summary>The first column with this exact header that is to the right of <paramref name="afterColumn"/>.</summary>
    public int? Find(string header, int afterColumn = 0)
    {
        foreach (var (text, column) in _headers)
        {
            if (column > afterColumn && string.Equals(text, header, _comparison))
            {
                return column;
            }
        }

        return null;
    }

    public bool Contains(string header) => Find(header) is not null;

    public IReadOnlyList<string> Missing(IEnumerable<string> required) =>
        required.Where(header => !Contains(header)).ToList();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
