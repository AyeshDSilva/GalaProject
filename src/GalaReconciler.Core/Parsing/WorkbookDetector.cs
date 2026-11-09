using ClosedXML.Excel;
using GalaReconciler.Core.Domain;

namespace GalaReconciler.Core.Parsing;

public enum WorkbookKind
{
    Unknown,
    Source,
    Processed,
}

/// <summary>
/// Tells the RSVP export and the master guest list apart by their headers, so users don't have to name
/// the files in any particular way.
/// </summary>
public static class WorkbookDetector
{
    public static WorkbookKind Detect(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return WorkbookKind.Unknown;
        }

        using (workbook)
        {
            // Any sheet with the export's ticket columns (matched exactly, like the parser does).
            foreach (var sheet in workbook.Worksheets.Where(s => s.LastRowUsed() is not null))
            {
                var map = new HeaderMap(sheet, StringComparison.Ordinal);
                if (map.Contains(SourceRsvpParser.Headers.TicketType) && map.Contains(SourceRsvpParser.Headers.OrderId))
                {
                    return WorkbookKind.Source;
                }
            }

            // The master list is read from the first sheet only.
            var first = workbook.Worksheets.FirstOrDefault();
            if (first?.LastRowUsed() is not null)
            {
                var map = new HeaderMap(first, StringComparison.OrdinalIgnoreCase);
                if (map.Contains(ProcessedGuestListParser.Headers.Surname)
                    && map.Contains(ProcessedGuestListParser.Headers.GuestName)
                    && EventDays.All.Any(day => map.Contains(day.ProcessedHeader)))
                {
                    return WorkbookKind.Processed;
                }
            }
        }

        return WorkbookKind.Unknown;
    }
}
