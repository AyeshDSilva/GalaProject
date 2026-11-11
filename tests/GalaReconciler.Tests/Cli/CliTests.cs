using ClosedXML.Excel;
using GalaReconciler.Cli;
using GalaReconciler.Core.Parsing;
using GalaReconciler.Tests.Helpers;

namespace GalaReconciler.Tests.Cli;

/// <summary>A temporary folder deleted afterwards.</summary>
public sealed class TempFolder : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("gala-cli-").FullName;

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void AddSamples(
        string sourceName = "RSVP export.xlsx", string processedName = "Master list.xlsx", string galaName = "Gala RSVP.xlsx")
    {
        SampleWorkbookBuilder.Source().SaveAs(File(sourceName));
        SampleWorkbookBuilder.Gala().SaveAs(File(galaName));
        SampleWorkbookBuilder.Processed().SaveAs(File(processedName));
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

public class CliOptionsTests
{
    [Fact]
    public void No_arguments_means_search_the_current_folder()
    {
        var options = CliOptions.Parse([]);

        Assert.Null(options.Error);
        Assert.Empty(options.Files);
        Assert.Null(options.Folder);
        Assert.False(options.StrictNotAttending);
    }

    [Fact]
    public void Parses_files_and_options()
    {
        var options = CliOptions.Parse(["a.xlsx", "b.xlsx", "c.xlsx", "--output", "out.xlsx", "--strict", "--no-pause", "--folder", "C:\\gala"]);

        Assert.Null(options.Error);
        Assert.Equal(["a.xlsx", "b.xlsx", "c.xlsx"], options.Files);
        Assert.Equal("out.xlsx", options.Output);
        Assert.Equal("C:\\gala", options.Folder);
        Assert.True(options.StrictNotAttending);
        Assert.True(options.NoPause);
    }

    [Theory]
    [InlineData("only-one.xlsx")]
    [InlineData("a.xlsx", "b.xlsx")]
    [InlineData("a.xlsx", "b.xlsx", "c.xlsx", "d.xlsx")]
    [InlineData("--output")]
    [InlineData("--bogus")]
    public void Reports_bad_arguments(params string[] args) => Assert.NotNull(CliOptions.Parse(args).Error);
}

public class WorkbookDetectorTests
{
    [Fact]
    public void Recognises_each_workbook_by_its_headers()
    {
        using var source = SampleWorkbookBuilder.Source().ToStream();
        using var gala = SampleWorkbookBuilder.Gala().ToStream();
        using var singleExport = SampleWorkbookBuilder.SingleExportSource().ToStream();
        using var processed = SampleWorkbookBuilder.Processed().ToStream();
        using var other = new WorkbookBuilder().Sheet("Sheet1", ["Name", "Phone"], [["A", "1"]]).ToStream();
        using var notAWorkbook = new MemoryStream("hello"u8.ToArray());

        Assert.Equal(WorkbookKind.Source, WorkbookDetector.Detect(source));
        Assert.Equal(WorkbookKind.GalaSource, WorkbookDetector.Detect(gala));
        Assert.Equal(WorkbookKind.Source, WorkbookDetector.Detect(singleExport)); // mostly day tickets
        Assert.Equal(WorkbookKind.Processed, WorkbookDetector.Detect(processed));
        Assert.Equal(WorkbookKind.Unknown, WorkbookDetector.Detect(other));
        Assert.Equal(WorkbookKind.Unknown, WorkbookDetector.Detect(notAWorkbook));
    }
}

public sealed class InputFileFinderTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Finds_all_three_files_whatever_they_are_called_and_ignores_other_files()
    {
        _folder.AddSamples("zzz.xlsx", "aaa.xlsx", "mmm.xlsx");
        new WorkbookBuilder().Sheet("Sheet1", ["Name"], [["x"]]).SaveAs(_folder.File("Budget.xlsx"));
        System.IO.File.WriteAllText(_folder.File("~$aaa.xlsx"), "Excel lock file");
        System.IO.File.WriteAllText(_folder.File("Guest List Reconciliation 2026-11-11 1200.xlsx"), "an earlier report");
        System.IO.File.WriteAllText(_folder.File("GALA Reconciliation 2026-11-09 1200.xlsx"), "a report from an earlier version");
        System.IO.File.WriteAllText(_folder.File("notes.txt"), "not a workbook");

        var inputs = InputFileFinder.Find(_folder.Path);

        Assert.Equal("zzz.xlsx", inputs.Source.Name);
        Assert.Equal("mmm.xlsx", inputs.Gala.Name);
        Assert.Equal("aaa.xlsx", inputs.Processed.Name);
    }

    [Fact]
    public void Explains_when_a_file_is_missing()
    {
        SampleWorkbookBuilder.Source().SaveAs(_folder.File("RSVP.xlsx"));
        SampleWorkbookBuilder.Gala().SaveAs(_folder.File("Gala.xlsx"));

        var ex = Assert.Throws<InputFileException>(() => InputFileFinder.Find(_folder.Path));
        Assert.Contains("master guest list", ex.Message);
        Assert.Contains("RSVP.xlsx", ex.Message);
    }

    [Fact]
    public void Explains_when_the_gala_export_is_missing()
    {
        SampleWorkbookBuilder.Source().SaveAs(_folder.File("RSVP.xlsx"));
        SampleWorkbookBuilder.Processed().SaveAs(_folder.File("Master.xlsx"));

        var ex = Assert.Throws<InputFileException>(() => InputFileFinder.Find(_folder.Path));
        Assert.Contains("Couldn't find the Gala RSVP export", ex.Message);
        Assert.Contains("RSVP - Attending", ex.Message);
    }

    [Fact]
    public void Refuses_to_guess_between_two_exports()
    {
        _folder.AddSamples();
        SampleWorkbookBuilder.Source().SaveAs(_folder.File("RSVP export (old).xlsx"));

        var ex = Assert.Throws<InputFileException>(() => InputFileFinder.Find(_folder.Path));
        Assert.Contains("more than one RSVP export", ex.Message);
    }

    [Fact]
    public void Explains_an_empty_folder() =>
        Assert.Contains("No .xlsx files", Assert.Throws<InputFileException>(() => InputFileFinder.Find(_folder.Path)).Message);
}

public sealed class ExcelReportWriterTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private XLWorkbook WriteSampleReport()
    {
        var path = _folder.File("report.xlsx");
        ExcelReportWriter.Write(TestHandlers.CompareSamples(), path);
        return new XLWorkbook(path);
    }

    [Fact]
    public void Has_summary_differences_and_warnings_sheets()
    {
        using var workbook = WriteSampleReport();

        Assert.Equal(
            [ExcelReportWriter.SummarySheet, ExcelReportWriter.DifferencesSheet, ExcelReportWriter.WarningsSheet],
            workbook.Worksheets.Select(s => s.Name));
    }

    [Fact]
    public void Summary_shows_the_files_and_counts()
    {
        using var workbook = WriteSampleReport();
        var summary = workbook.Worksheet(ExcelReportWriter.SummarySheet);
        string ValueFor(string label) =>
            summary.Column(1).CellsUsed().Single(c => c.GetString() == label).CellRight().GetFormattedString();

        Assert.Equal("Source.xlsx", ValueFor("Source file"));
        Assert.Equal("Gala.xlsx", ValueFor("Gala file"));
        Assert.Equal("Processed.xlsx", ValueFor("Processed file"));
        Assert.Equal("14", ValueFor("Matched"));
        Assert.Equal("3", ValueFor("Guest not found"));
        Assert.Equal("3", ValueFor("Dates mismatch"));
        Assert.Equal("1", ValueFor("Dietary mismatch"));
        Assert.Equal("1", ValueFor("Accessibility mismatch"));
        Assert.Equal("14", ValueFor("Total"));
        Assert.Equal("1", ValueFor("Mobile mismatch"));
        Assert.Equal("1", ValueFor("Position mismatch"));
        Assert.Equal("1", ValueFor("Warnings"));
    }

    [Fact]
    public void Differences_are_a_filterable_table_with_one_row_each()
    {
        using var workbook = WriteSampleReport();
        var sheet = workbook.Worksheet(ExcelReportWriter.DifferencesSheet);

        var table = sheet.Tables.Single();
        Assert.True(table.ShowAutoFilter);
        Assert.Equal(14, table.DataRange.RowCount());
        Assert.Equal(
            ["Type", "Guest", "Name in Source", "Guest of", "Details", "Possible match", "Matched by", "Source", "Processed",
             "Thu", "Fri", "Sat", "Sun", "Gala", "Source location", "Processed location"],
            table.Fields.Select(f => f.Name));

        IXLTableRow Row(string guest, string type) =>
            table.DataRange.Rows().Single(r => r.Field("Guest").GetString() == guest && r.Field("Type").GetString() == type);

        // William's numbers, with the location of his two Source rows.
        var william = Row("William Hartley", "Mobile mismatch");
        Assert.Equal("Nothing in common", william.Field("Details").GetString());
        Assert.Equal("0491576398\n0491576801", william.Field("Processed").GetString().ReplaceLineEndings("\n"));
        Assert.Equal("Thurs_Sun Suite RSVP · rows 17, 18 · Order BH77KWER", william.Field("Source location").GetString());
        Assert.Equal("Sheet1 · row 14", william.Field("Processed location").GetString());
        Assert.Equal("", william.Field("Gala").GetString());

        // Clara is attending but still marked 1 (invited, no response) for the Gala: that day differs and is highlighted.
        var clara = Row("Clara Walsh", "Dates mismatch");
        Assert.Equal("✓ → ?", clara.Field("Gala").GetString());
        Assert.Equal(XLColor.FromHtml("#FDE68A"), clara.Field("Gala").Style.Fill.BackgroundColor);
        Assert.Equal("– → –", clara.Field("Sat").GetString());
        Assert.NotEqual(XLColor.FromHtml("#FDE68A"), clara.Field("Sat").Style.Fill.BackgroundColor);
        Assert.Equal("attendee-report (Gala RSVP) · row 4 · Order GG20TS02", clara.Field("Source location").GetString());

        // Tom is "RSVP - Not attending" in the Gala export but still Y in the master list.
        var tom = Row("Tom Baker", "Dates mismatch");
        Assert.Equal("✗ → ✓", tom.Field("Gala").GetString());
        Assert.Equal(XLColor.FromHtml("#FDE68A"), tom.Field("Gala").Style.Fill.BackgroundColor);
        Assert.Equal("Gala: Not attending", tom.Field("Source").GetString());
        Assert.Equal("Gala: Attending", tom.Field("Processed").GetString());

        // Grace is not attending and N: no Grid Gala row. Her accessibility requirement is missing from the master list.
        Assert.DoesNotContain(table.DataRange.Rows(), r => r.Field("Guest").GetString() == "Grace Huang" && r.Field("Type").GetString() == "Dates mismatch");
        var grace = Row("Grace Huang", "Accessibility mismatch");
        Assert.Equal("Missing in Processed", grace.Field("Details").GetString());
        Assert.Equal("Hearing loop", grace.Field("Source").GetString());

        var olivia = Row("Olivia Grant-Taylor", "Dietary mismatch");
        Assert.Equal("Not in Processed: No dairy", olivia.Field("Details").GetString());
        Assert.Equal("Gluten-free", olivia.Field("Processed").GetString());

        Assert.Equal("Dr", Row("Leilani Fonua", "Title mismatch").Field("Source").GetString());
        Assert.Equal("Director", Row("Henry Walsh", "Position mismatch").Field("Processed").GetString());

        Assert.Equal("Email", Row("William Hartley", "Dates mismatch").Field("Matched by").GetString());
        Assert.Equal("Bill Hartley", Row("William Hartley", "Dates mismatch").Field("Name in Source").GetString());
        Assert.Equal("Ruby McIntyre", Row("Ruby Macintyre", "Guest not found").Field("Possible match").GetString());
    }

    [Fact]
    public void Warnings_sheet_lists_each_warning()
    {
        using var workbook = WriteSampleReport();
        var table = workbook.Worksheet(ExcelReportWriter.WarningsSheet).Tables.Single();

        var row = Assert.Single(table.DataRange.Rows());
        Assert.Equal("Source", row.Field("File").GetString());
        Assert.Contains("Sunday waitlist", row.Field("Warning").GetString());
    }
}

public sealed class CliAppTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly StringWriter _output = new();

    public void Dispose() => _folder.Dispose();

    private int Run(params string[] args) =>
        new CliApp(TestHandlers.CreateConsoleCompareFilesHandler(), _output).Run(CliOptions.Parse(args), _folder.Path);

    private string[] Reports() =>
        Directory.GetFiles(_folder.Path, $"{InputFileFinder.ReportFilePrefix}*.xlsx").Select(Path.GetFileName).Order().ToArray()!;

    [Fact]
    public void Writes_results_and_a_highlighted_copy_and_leaves_the_originals_alone()
    {
        _folder.AddSamples();
        var sourceBefore = File.ReadAllBytes(_folder.File("RSVP export.xlsx"));
        var processedBefore = File.ReadAllBytes(_folder.File("Master list.xlsx"));

        Assert.Equal(ExitCodes.Success, Run());

        var reports = Reports();
        Assert.Equal(2, reports.Length);
        var results = Assert.Single(reports, r => !r.Contains("(highlighted)"));
        Assert.Matches(@"^Guest List Reconciliation \d{4}-\d{2}-\d{2} \d{4}\.xlsx$", results);
        Assert.Contains($"{Path.GetFileNameWithoutExtension(results)} - Master list (highlighted).xlsx", reports);
        Assert.Equal(sourceBefore, File.ReadAllBytes(_folder.File("RSVP export.xlsx")));
        Assert.Equal(processedBefore, File.ReadAllBytes(_folder.File("Master list.xlsx")));

        var text = _output.ToString();
        Assert.Contains("Source file:    RSVP export.xlsx", text);
        Assert.Contains("Gala file:      Gala RSVP.xlsx", text);
        Assert.Contains("Processed file: Master list.xlsx", text);
        Assert.Contains(
            "14 differences: 3 guest not found, 3 dates mismatch, 1 mobile mismatch, 2 email mismatch, 1 title mismatch, 1 post nominal mismatch, 1 position mismatch, 1 dietary mismatch, 1 accessibility mismatch",
            text);
        Assert.Contains("Results saved:", text);
        Assert.Contains("Highlighted master list:", text);
        Assert.Contains("(13 cells highlighted; the original files were not changed)", text);
    }

    [Fact]
    public void Running_again_keeps_the_earlier_files_and_ignores_them_when_searching()
    {
        _folder.AddSamples();

        Assert.Equal(ExitCodes.Success, Run());
        Assert.Equal(ExitCodes.Success, Run());

        Assert.Equal(4, Reports().Length);
    }

    [Fact]
    public void Accepts_the_three_file_names_in_any_order()
    {
        _folder.AddSamples();

        Assert.Equal(ExitCodes.Success, Run("Master list.xlsx", "Gala RSVP.xlsx", "RSVP export.xlsx", "--output", "out.xlsx"));

        var text = _output.ToString();
        Assert.Contains("Source file:    RSVP export.xlsx", text);
        Assert.Contains("Gala file:      Gala RSVP.xlsx", text);
        Assert.Contains("14 differences", text);
        Assert.True(File.Exists(_folder.File("out.xlsx")));
        Assert.True(File.Exists(_folder.File("out - Master list (highlighted).xlsx")));
    }

    [Fact]
    public void Says_which_named_file_is_missing()
    {
        _folder.AddSamples();
        new WorkbookBuilder().Sheet("Sheet1", ["Name"], [["x"]]).SaveAs(_folder.File("Budget.xlsx"));

        Assert.Equal(ExitCodes.InvalidInput, Run("Master list.xlsx", "RSVP export.xlsx", "Budget.xlsx"));

        Assert.Contains("None of the files given is the Gala RSVP export", _output.ToString());
        Assert.Empty(Reports());
    }

    [Fact]
    public void Highlighted_copy_with_a_custom_name_is_not_mistaken_for_a_master_list()
    {
        _folder.AddSamples();
        Assert.Equal(ExitCodes.Success, Run("--output", "mine.xlsx"));

        Assert.Equal(ExitCodes.Success, Run());
    }

    [Fact]
    public void Explains_a_problem_with_the_files_and_returns_an_error_code()
    {
        SampleWorkbookBuilder.Processed().SaveAs(_folder.File("Master list.xlsx"));

        Assert.Equal(ExitCodes.InvalidInput, Run());

        Assert.Contains("Couldn't find the RSVP export", _output.ToString());
        Assert.Empty(Reports());
    }

    [Fact]
    public void Output_paths_never_overwrite_and_accept_a_folder()
    {
        var runAt = new DateTimeOffset(2026, 11, 9, 14, 30, 0, TimeSpan.Zero);
        OutputPaths Paths(string? requested, string folder) => OutputPaths.For(requested, _folder.Path, folder, runAt, "Master list.xlsx");

        var first = Paths(null, _folder.Path);
        Assert.Equal(_folder.File("Guest List Reconciliation 2026-11-09 1430.xlsx"), first.Results);
        Assert.Equal(_folder.File("Guest List Reconciliation 2026-11-09 1430 - Master list (highlighted).xlsx"), first.HighlightedCopy);

        // Either file existing moves both on to the next number, so they always pair up.
        File.WriteAllText(first.HighlightedCopy, "existing");
        var second = Paths(null, _folder.Path);
        Assert.Equal(_folder.File("Guest List Reconciliation 2026-11-09 1430 (2).xlsx"), second.Results);
        Assert.Equal(_folder.File("Guest List Reconciliation 2026-11-09 1430 (2) - Master list (highlighted).xlsx"), second.HighlightedCopy);

        Assert.Equal(_folder.File("mine.xlsx"), Paths("mine", _folder.Path).Results);
        Assert.Equal(_folder.File("mine - Master list (highlighted).xlsx"), Paths("mine", _folder.Path).HighlightedCopy);
        Assert.Equal(second.Results, Paths(".", "C:\\elsewhere").Results);
    }
}

public sealed class ProcessedFileHighlighterTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private (XLWorkbook Workbook, int Count) Highlight()
    {
        var path = _folder.File("highlighted.xlsx");
        var count = ProcessedFileHighlighter.Write(SampleWorkbookBuilder.Processed().ToStream().ToArray(), TestHandlers.CompareSamples(), path);
        return (new XLWorkbook(path), count);
    }

    /// <summary>The cell in <paramref name="row"/> under the column headed <paramref name="header"/>.</summary>
    private static IXLCell Cell(IXLWorksheet sheet, int row, string header) =>
        sheet.Cell(row, sheet.Row(1).CellsUsed().Single(c => c.GetString() == header).Address.ColumnNumber);

    /// <summary>True when the highlighter coloured this cell yellow (differs) or red (not in the export).</summary>
    private static bool IsMarked(IXLCell cell) =>
        cell.Style.Fill.BackgroundColor == ProcessedFileHighlighter.ChangedFill
        || cell.Style.Fill.BackgroundColor == ProcessedFileHighlighter.NotInExportFill;

    [Fact]
    public void Marks_every_difference_on_the_right_cell()
    {
        var (workbook, count) = Highlight();
        using var _ = workbook;
        var sheet = workbook.Worksheet(1);
        var yellow = ProcessedFileHighlighter.ChangedFill;

        // Rows: 3 Leilani, 5 Olivia, 7 Ethan, 11 Henry, 12 Clara, 14 William, 15 Ruby McIntyre, 16 Grace, 17 Tom.
        Assert.Equal(13, count);

        // Dietary and accessibility.
        Assert.Equal(yellow, Cell(sheet, 5, "Dietary").Style.Fill.BackgroundColor);        // Olivia: "No dairy" missing
        Assert.Equal(yellow, Cell(sheet, 16, "Accessibility").Style.Fill.BackgroundColor); // Grace: "Hearing loop" missing
        Assert.False(IsMarked(Cell(sheet, 2, "Dietary")));        // Marco: "Other" + "No shellfish" matches
        Assert.False(IsMarked(Cell(sheet, 4, "Dietary")));        // Alice: Vegetarian in both
        Assert.False(IsMarked(Cell(sheet, 11, "Accessibility"))); // Henry: "Yes" + "Wheelchair access" is in his cell
        Assert.False(IsMarked(Cell(sheet, 8, "Dietary")));        // Luca: his unnamed guest's "Vegan" isn't his

        // William's Phone cell has two numbers on separate lines and neither is in the export.
        Assert.Equal(yellow, Cell(sheet, 14, "Phone").Style.Fill.BackgroundColor);
        Assert.Equal(yellow, Cell(sheet, 3, "Prenom").Style.Fill.BackgroundColor);
        Assert.Equal(yellow, Cell(sheet, 5, "Postnom").Style.Fill.BackgroundColor);
        Assert.Equal(yellow, Cell(sheet, 11, "Position").Style.Fill.BackgroundColor);
        Assert.Equal(yellow, Cell(sheet, 7, "Email 1").Style.Fill.BackgroundColor);

        // Days: only the differing day is marked.
        Assert.Equal(yellow, Cell(sheet, 14, "SAT").Style.Fill.BackgroundColor);
        Assert.False(IsMarked(Cell(sheet, 14, "FRI")));
        Assert.Equal(yellow, Cell(sheet, 12, "Grid Gala").Style.Fill.BackgroundColor); // Clara: attending, but 1

        // Grid Gala against the Gala export: "RSVP - Not attending" must be N.
        Assert.Equal(yellow, Cell(sheet, 17, "Grid Gala").Style.Fill.BackgroundColor); // Tom: not attending, but Y
        Assert.False(IsMarked(Cell(sheet, 16, "Grid Gala"))); // Grace: not attending and N agree
        Assert.False(IsMarked(Cell(sheet, 13, "Grid Gala"))); // Felix: attending and Y agree

        // In the master list but not the export: name in red, for main guests only.
        Assert.Equal(ProcessedFileHighlighter.NotInExportFill, Cell(sheet, 15, "First Name").Style.Fill.BackgroundColor);
        Assert.Equal(ProcessedFileHighlighter.NotInExportFill, Cell(sheet, 15, "Surname").Style.Fill.BackgroundColor);
        Assert.False(IsMarked(Cell(sheet, 10, "First Name"))); // Maeve: guest of Oscar Reid
        Assert.False(IsMarked(Cell(sheet, 10, "Surname")));
    }

    [Fact]
    public void Adds_no_notes_to_the_highlighted_cells()
    {
        var (workbook, _) = Highlight();
        using var _workbook = workbook;

        Assert.DoesNotContain(workbook.Worksheet(1).CellsUsed(), c => c.HasComment);
    }

    [Fact]
    public void Leaves_alone_what_shouldnt_change()
    {
        var (workbook, _) = Highlight();
        using var _workbook = workbook;
        var sheet = workbook.Worksheet(1);

        Assert.False(IsMarked(Cell(sheet, 9, "Position")));   // Oscar: export says "nil"
        Assert.False(IsMarked(Cell(sheet, 3, "Position")));   // Leilani: "Guest of …" isn't a job title
        Assert.False(IsMarked(Cell(sheet, 4, "Prenom")));     // Alice: "Ms." = "Ms"
        Assert.False(IsMarked(Cell(sheet, 7, "Grid Gala")));  // Ethan: 1 and no ticket agree
        Assert.False(IsMarked(Cell(sheet, 7, "First Name"))); // Ethan: his "guest" has no ticket, which isn't reported
        Assert.False(IsMarked(Cell(sheet, 7, "Guest Name")));
        Assert.False(IsMarked(Cell(sheet, 4, "First Name")));

        // One matching phone or email is enough.
        Assert.False(IsMarked(Cell(sheet, 2, "Phone")));      // Marco: one of his export numbers has a typo
        Assert.False(IsMarked(Cell(sheet, 3, "Phone")));      // Leilani: one of her two export numbers matches
        Assert.False(IsMarked(Cell(sheet, 4, "Phone")));      // Alice: one of the two numbers in the cell matches
        Assert.False(IsMarked(Cell(sheet, 8, "Email 1")));    // Luca: his export email is in Email 2
        Assert.False(IsMarked(Cell(sheet, 8, "Email 2")));

        // Everything else in the list is kept, including hidden columns and other data.
        Assert.True(sheet.Column(2).IsHidden);
        Assert.Equal("Wheelchair access", Cell(sheet, 10, "Accessibility").GetString());
    }
}
