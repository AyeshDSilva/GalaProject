using ClosedXML.Excel;
using GalaReconciler.Core.Domain;

namespace GalaReconciler.Core.Parsing;

public enum WorkbookKind
{
    Unknown,
    Source,
    Processed,

    /// <summary>The separate Gala RSVP export ("RSVP - Attending" / "RSVP - Not attending").</summary>
    GalaSource,
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
            // Any sheet with the export's ticket columns (matched exactly, like the parser does). The main export
            // and the Gala export both have them; the Gala export's ticket types are "RSVP - (Not) attending"
            // rather than "<day> (not) attending".
            var isExport = false;
            int galaTickets = 0, dayTickets = 0;
            foreach (var sheet in workbook.Worksheets.Where(s => s.LastRowUsed() is not null))
            {
                var map = new HeaderMap(sheet, StringComparison.Ordinal);
                if (map.Find(SourceRsvpParser.Headers.TicketType) is not { } ticketType || !map.Contains(SourceRsvpParser.Headers.OrderId))
                {
                    continue;
                }

                isExport = true;
                var lastRow = sheet.LastRowUsed()!.RowNumber();
                for (var row = 2; row <= lastRow; row++)
                {
                    var text = CellText.Read(sheet.Row(row), ticketType);
                    if (TicketTypeParser.IsGalaRsvp(text))
                    {
                        galaTickets++;
                    }
                    else if (TicketTypeParser.TryParse(text, out _, out _))
                    {
                        dayTickets++;
                    }
                }
            }

            if (isExport)
            {
                return galaTickets > dayTickets ? WorkbookKind.GalaSource : WorkbookKind.Source;
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
