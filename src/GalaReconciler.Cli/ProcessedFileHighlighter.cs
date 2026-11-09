using ClosedXML.Excel;
using GalaReconciler.Core;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Parsing;
using static GalaReconciler.Core.Parsing.ProcessedGuestListParser;

namespace GalaReconciler.Cli;

/// <summary>
/// Saves a copy of the master guest list with every difference marked: changed cells in yellow with a note
/// giving the RSVP export's value, and the names of people who aren't in the export in red. The original file
/// is never modified.
/// </summary>
public static class ProcessedFileHighlighter
{
    public const string NoteAuthor = "GALA Reconciler";

    public static readonly XLColor ChangedFill = XLColor.FromHtml("#FFF59D");
    public static readonly XLColor NotInExportFill = XLColor.FromHtml("#FECACA");

    /// <returns>How many cells were highlighted.</returns>
    public static int Write(byte[] processedFile, ReconciliationResult result, string path)
    {
        using var stream = new MemoryStream(processedFile, writable: false);
        using var workbook = new XLWorkbook(stream);

        // The parser only reads the first sheet, so all row references point there.
        var sheet = workbook.Worksheet(1);
        var map = new HeaderMap(sheet, StringComparison.OrdinalIgnoreCase);
        var highlighted = new HashSet<IXLCell>();

        foreach (var discrepancy in result.Discrepancies)
        {
            foreach (var (header, fill, note) in Marks(discrepancy, sheet, map))
            {
                if (map.Find(header) is not { } column)
                {
                    continue;
                }

                foreach (var row in discrepancy.ProcessedRows.Where(r => r.SheetName == sheet.Name))
                {
                    var cell = sheet.Cell(row.RowNumber, column);
                    cell.Style.Fill.BackgroundColor = fill;
                    AddNote(cell, note);
                    highlighted.Add(cell);
                }
            }
        }

        workbook.SaveAs(path);
        return highlighted.Count;
    }

    /// <summary>Which columns to mark for a difference, how, and what the note says.</summary>
    private static IEnumerable<(string Header, XLColor Fill, string Note)> Marks(Discrepancy d, IXLWorksheet sheet, HeaderMap map)
    {
        var who = d.GuestDisplayName;
        switch (d.Type)
        {
            case DiscrepancyType.GuestNotFound:
                var note = $"{who} is not in the RSVP export.";
                if (d.PossibleMatch is not null)
                {
                    note += $" Possible match there: {d.PossibleMatch}.";
                }

                yield return (Headers.FirstName, NotInExportFill, note);
                yield return (Headers.Surname, NotInExportFill, note);
                break;

            case DiscrepancyType.DatesMismatch:
                foreach (var day in d.Days.Where(day => day.IsDifferent))
                {
                    var info = EventDays.Get(day.Day);
                    yield return (info.ProcessedHeader, ChangedFill,
                        $"{who}, {info.TicketTypeLabel}\nRSVP export: {day.Source.ToDisplay()}\nThis list: {day.Processed.ToDisplay()}");
                }

                break;

            case DiscrepancyType.MobileNumberMismatch:
                yield return (Headers.Phone, ChangedFill, ValuesNote(d, "Phone"));
                break;

            case DiscrepancyType.EmailMismatch:
                var emailNote = ValuesNote(d, "Email");
                yield return (Headers.Email1, ChangedFill, emailNote);

                // Email 2 too when it holds an address, since the difference may be in either.
                if (map.Find(Headers.Email2) is { } email2 && d.ProcessedRows.Any(r => !sheet.Cell(r.RowNumber, email2).IsEmpty()))
                {
                    yield return (Headers.Email2, ChangedFill, emailNote);
                }

                break;

            case DiscrepancyType.TitleMismatch:
                yield return (Headers.Prenom, ChangedFill, ValuesNote(d, "Title"));
                break;

            case DiscrepancyType.PostNominalMismatch:
                yield return (Headers.Postnom, ChangedFill, ValuesNote(d, "Post nominal"));
                break;

            case DiscrepancyType.PositionMismatch:
                yield return (Headers.Position, ChangedFill, ValuesNote(d, "Position title"));
                break;
        }
    }

    private static string ValuesNote(Discrepancy d, string field)
    {
        static string Show(string? value) => string.IsNullOrEmpty(value) ? "(blank)" : value.Replace("\n", ", ");
        return $"{d.GuestDisplayName}, {field}\nRSVP export: {Show(d.SourceValue)}\nThis list: {Show(d.ProcessedValue)}";
    }

    /// <summary>Adds to any note already on the cell (the list's own, or another difference's).</summary>
    private static void AddNote(IXLCell cell, string text)
    {
        if (cell.HasComment)
        {
            cell.GetComment().AddNewLine().AddNewLine().AddText(text);
            return;
        }

        var comment = cell.CreateComment();
        comment.Author = NoteAuthor;
        comment.AddText(text);
        comment.Style.Size.SetWidth(36).Size.SetHeight(8);
    }
}
