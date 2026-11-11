using ClosedXML.Excel;
using GalaReconciler.Core;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;

namespace GalaReconciler.Cli;

/// <summary>Writes a comparison result as an Excel workbook: Summary, Differences and Warnings sheets.</summary>
public static class ExcelReportWriter
{
    public const string SummarySheet = "Summary";
    public const string DifferencesSheet = "Differences";
    public const string WarningsSheet = "Warnings";

    private static readonly XLColor HeaderFill = XLColor.FromHtml("#F1F5F9");
    private static readonly XLColor DifferingDayFill = XLColor.FromHtml("#FDE68A");
    private static readonly XLColor MutedText = XLColor.FromHtml("#64748B");

    /// <param name="highlightedCopyName">File name of the highlighted master list, mentioned on the Summary sheet.</param>
    public static void Write(ReconciliationResult result, string path, string? highlightedCopyName = null)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Author = "GALA Reconciler";
        workbook.Properties.Title = "GALA guest list reconciliation";

        AddSummary(workbook.AddWorksheet(SummarySheet), result, highlightedCopyName);
        AddDifferences(workbook.AddWorksheet(DifferencesSheet), result.Discrepancies);
        AddWarnings(workbook.AddWorksheet(WarningsSheet), result.Warnings);

        workbook.Worksheet(SummarySheet).SetTabActive();
        workbook.SaveAs(path);
    }

    /// <summary>"✓" attending, "✗" not attending, "?" invited with no response, "–" no RSVP or not invited.</summary>
    public static string Symbol(AttendanceState state) => state switch
    {
        AttendanceState.Attending => "✓",
        AttendanceState.NotAttending => "✗",
        AttendanceState.AwaitingResponse => "?",
        _ => "–",
    };

    private static void AddSummary(IXLWorksheet sheet, ReconciliationResult result, string? highlightedCopyName)
    {
        sheet.Cell("A1").Value = "GALA guest list reconciliation";
        sheet.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16);

        var row = 3;
        void Line(string label, XLCellValue value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.FontColor = MutedText;
            sheet.Cell(row, 2).Value = value;
            sheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            row++;
        }

        Line("Source file", result.SourceFileName);
        if (result.GalaFileName is not null)
        {
            Line("Gala file", result.GalaFileName);
        }

        Line("Processed file", result.ProcessedFileName);
        if (highlightedCopyName is not null)
        {
            Line("Highlighted copy", highlightedCopyName);
        }

        Line("Compared", result.RunAt.ToString("d MMM yyyy, h:mm tt"));
        Line("Guests in Source", result.SourceGuestCount);
        Line("Guests in Processed", result.ProcessedGuestCount);
        Line("Matched", result.MatchedCount);

        row++;
        sheet.Cell(row, 1).Value = "Differences";
        sheet.Cell(row, 1).Style.Font.SetBold();
        row++;
        foreach (var type in Enum.GetValues<DiscrepancyType>())
        {
            var count = result.Discrepancies.Count(d => d.Type == type);
            sheet.Cell(row, 1).Value = type.ToDisplay();
            StyleType(sheet.Cell(row, 1), type);
            sheet.Cell(row, 2).Value = count;
            sheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            row++;
        }

        sheet.Cell(row, 1).Value = "Total";
        sheet.Cell(row, 1).Style.Font.SetBold();
        sheet.Cell(row, 2).Value = result.Discrepancies.Count;
        sheet.Cell(row, 2).Style.Font.SetBold().Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        row += 2;

        Line("Warnings", result.Warnings.Count);
        row++;

        var notes = new[]
        {
            $"Every difference is listed on the {DifferencesSheet} sheet; use the filter arrows to show one type.",
            "Day columns, shown as Source → Processed: ✓ attending (Y), ✗ not attending (N), ? invited with no response yet (1), – no RSVP in the export or not invited (blank). Highlighted days differ.",
            "Location gives the sheet and row numbers in each original file.",
        };
        foreach (var note in notes)
        {
            sheet.Cell(row, 1).Value = note;
            sheet.Cell(row, 1).Style.Font.FontColor = MutedText;
            row++;
        }

        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 60;
    }

    private static void AddDifferences(IXLWorksheet sheet, IReadOnlyList<Discrepancy> discrepancies)
    {
        if (discrepancies.Count == 0)
        {
            sheet.Cell("A1").Value = "No differences found: every matched guest has the same days, mobile numbers and emails in both files.";
            return;
        }

        var days = EventDays.All;
        string[] headers =
        [
            "Type", "Guest", "Name in Source", "Guest of", "Details", "Possible match", "Matched by",
            "Source", "Processed", .. days.Select(d => d.ShortLabel), "Source location", "Processed location",
        ];
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        var firstDayColumn = 10;
        var row = 2;
        foreach (var d in discrepancies)
        {
            sheet.Cell(row, 1).Value = d.Type.ToDisplay();
            StyleType(sheet.Cell(row, 1), d.Type);
            sheet.Cell(row, 2).Value = d.GuestDisplayName;
            sheet.Cell(row, 3).Value = d.SourceDisplayName ?? "";
            sheet.Cell(row, 4).Value = d.HostDisplayName ?? "";
            sheet.Cell(row, 5).Value = d.Detail;
            sheet.Cell(row, 6).Value = d.PossibleMatch ?? "";
            sheet.Cell(row, 7).Value = d.MatchedBy switch
            {
                MatchMethod.Email => "Email",
                MatchMethod.GuestOfHost => "Guest of host",
                _ => "",
            };
            sheet.Cell(row, 8).Value = d.SourceValue ?? "";
            sheet.Cell(row, 9).Value = d.ProcessedValue ?? "";

            foreach (var day in d.Days)
            {
                var cell = sheet.Cell(row, firstDayColumn + days.ToList().FindIndex(info => info.Day == day.Day));
                cell.Value = $"{Symbol(day.Source)} → {Symbol(day.Processed)}";
                if (day.IsDifferent)
                {
                    cell.Style.Fill.BackgroundColor = DifferingDayFill;
                    cell.Style.Font.SetBold();
                }
            }

            var locationColumn = firstDayColumn + days.Count;
            sheet.Cell(row, locationColumn).Value = string.Join("\n", RowReference.Describe(d.SourceRows));
            sheet.Cell(row, locationColumn + 1).Value = string.Join("\n", RowReference.Describe(d.ProcessedRows));
            row++;
        }

        var table = sheet.Range(1, 1, row - 1, headers.Length).CreateTable(DifferencesSheet);
        table.Theme = XLTableTheme.TableStyleLight1;
        table.ShowAutoFilter = true;

        var used = sheet.Range(1, 1, row - 1, headers.Length);
        used.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        used.Style.Alignment.WrapText = true;
        sheet.Range(1, 1, 1, headers.Length).Style.Font.SetBold().Fill.SetBackgroundColor(HeaderFill);
        sheet.Range(1, firstDayColumn, row - 1, firstDayColumn + days.Count - 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.SheetView.FreezeRows(1);

        double[] widths = [17, 24, 22, 22, 40, 22, 11, 28, 28, .. days.Select(_ => 8.0), 38, 26];
        for (var column = 0; column < widths.Length; column++)
        {
            sheet.Column(column + 1).Width = widths[column];
        }
    }

    private static void AddWarnings(IXLWorksheet sheet, IReadOnlyList<ReconciliationWarning> warnings)
    {
        if (warnings.Count == 0)
        {
            sheet.Cell("A1").Value = "No warnings.";
            return;
        }

        sheet.Cell(1, 1).Value = "File";
        sheet.Cell(1, 2).Value = "Warning";
        for (var i = 0; i < warnings.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = warnings[i].File;
            sheet.Cell(i + 2, 2).Value = warnings[i].Message;
        }

        var table = sheet.Range(1, 1, warnings.Count + 1, 2).CreateTable(WarningsSheet);
        table.Theme = XLTableTheme.TableStyleLight1;
        sheet.Range(1, 1, 1, 2).Style.Font.SetBold().Fill.SetBackgroundColor(HeaderFill);
        sheet.Range(1, 1, warnings.Count + 1, 2).Style.Alignment.WrapText = true;
        sheet.Range(1, 1, warnings.Count + 1, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.SheetView.FreezeRows(1);
        sheet.Column(1).Width = 12;
        sheet.Column(2).Width = 110;
    }

    private static void StyleType(IXLCell cell, DiscrepancyType type)
    {
        var (fill, text) = type switch
        {
            DiscrepancyType.GuestNotFound => ("#FFE4E6", "#BE123C"),
            DiscrepancyType.DatesMismatch => ("#FEF3C7", "#92400E"),
            DiscrepancyType.MobileNumberMismatch => ("#E0F2FE", "#0369A1"),
            DiscrepancyType.EmailMismatch => ("#EDE9FE", "#6D28D9"),
            DiscrepancyType.TitleMismatch => ("#CCFBF1", "#0F766E"),
            DiscrepancyType.PostNominalMismatch => ("#FAE8FF", "#A21CAF"),
            _ => ("#FFEDD5", "#C2410C"),
        };
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(fill);
        cell.Style.Font.FontColor = XLColor.FromHtml(text);
        cell.Style.Font.SetBold();
    }
}
