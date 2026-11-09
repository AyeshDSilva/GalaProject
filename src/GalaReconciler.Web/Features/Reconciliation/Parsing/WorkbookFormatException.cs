using ClosedXML.Excel;

namespace GalaReconciler.Web.Features.Reconciliation.Parsing;

/// <summary>A file can't be compared (not a workbook, no data, missing headers). The message is safe to show.</summary>
public sealed class WorkbookFormatException(
    string fileLabel,
    string message,
    IReadOnlyList<string>? missingHeaders = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public string FileLabel { get; } = fileLabel;

    public IReadOnlyList<string> MissingHeaders { get; } = missingHeaders ?? [];

    internal static XLWorkbook OpenWorkbook(Stream stream, string fileLabel)
    {
        try
        {
            return new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new WorkbookFormatException(
                fileLabel,
                $"The {fileLabel} couldn't be opened. Make sure it is a valid .xlsx workbook (not .xls or .csv) and isn't password protected.",
                innerException: ex);
        }
    }
}
