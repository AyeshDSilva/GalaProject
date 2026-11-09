using ClosedXML.Excel;

namespace GalaReconciler.Tests.Helpers;

/// <summary>Builds small in-memory workbooks for tests.</summary>
public sealed class WorkbookBuilder
{
    private readonly XLWorkbook _workbook = new();

    public WorkbookBuilder Sheet(
        string name,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows,
        Action<IXLWorksheet>? configure = null)
    {
        var sheet = _workbook.AddWorksheet(name);
        for (var column = 0; column < headers.Count; column++)
        {
            SetValue(sheet.Cell(1, column + 1), headers[column]);
        }

        var rowNumber = 2;
        foreach (var row in rows)
        {
            for (var column = 0; column < row.Count; column++)
            {
                SetValue(sheet.Cell(rowNumber, column + 1), row[column]);
            }

            rowNumber++;
        }

        configure?.Invoke(sheet);
        return this;
    }

    public MemoryStream ToStream()
    {
        var stream = new MemoryStream();
        _workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    public void SaveAs(string path) => _workbook.SaveAs(path);

    private static void SetValue(IXLCell cell, object? value) =>
        cell.Value = value switch
        {
            null => Blank.Value,
            "" => Blank.Value,
            string text => text,
            int number => (double)number,
            long number => (double)number,
            double number => number,
            decimal number => (double)number,
            _ => value.ToString(),
        };
}
