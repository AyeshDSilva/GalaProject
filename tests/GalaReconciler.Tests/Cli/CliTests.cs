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

    public void AddSamples(string sourceName = "RSVP export.xlsx", string processedName = "Master list.xlsx")
    {
        SampleWorkbookBuilder.Source().SaveAs(File(sourceName));
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
        Assert.Null(options.SourceFile);
        Assert.Null(options.Folder);
        Assert.False(options.StrictNotAttending);
    }

    [Fact]
    public void Parses_files_and_options()
    {
        var options = CliOptions.Parse(["a.xlsx", "b.xlsx", "--output", "out.xlsx", "--strict", "--no-pause", "--folder", "C:\\gala"]);

        Assert.Null(options.Error);
        Assert.Equal("a.xlsx", options.SourceFile);
        Assert.Equal("b.xlsx", options.ProcessedFile);
        Assert.Equal("out.xlsx", options.Output);
        Assert.Equal("C:\\gala", options.Folder);
        Assert.True(options.StrictNotAttending);
        Assert.True(options.NoPause);
    }

    [Theory]
    [InlineData("only-one.xlsx")]
    [InlineData("a.xlsx", "b.xlsx", "c.xlsx")]
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
        using var processed = SampleWorkbookBuilder.Processed().ToStream();
        using var other = new WorkbookBuilder().Sheet("Sheet1", ["Name", "Phone"], [["A", "1"]]).ToStream();
        using var notAWorkbook = new MemoryStream("hello"u8.ToArray());

        Assert.Equal(WorkbookKind.Source, WorkbookDetector.Detect(source));
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
    public void Finds_both_files_whatever_they_are_called_and_ignores_other_files()
    {
        _folder.AddSamples("zzz.xlsx", "aaa.xlsx");
        new WorkbookBuilder().Sheet("Sheet1", ["Name"], [["x"]]).SaveAs(_folder.File("Budget.xlsx"));
        System.IO.File.WriteAllText(_folder.File("~$aaa.xlsx"), "Excel lock file");
        System.IO.File.WriteAllText(_folder.File("GALA Reconciliation 2026-11-09 1200.xlsx"), "an earlier report");
        System.IO.File.WriteAllText(_folder.File("notes.txt"), "not a workbook");

        var inputs = InputFileFinder.Find(_folder.Path);

        Assert.Equal("zzz.xlsx", inputs.Source.Name);
        Assert.Equal("aaa.xlsx", inputs.Processed.Name);
    }

    [Fact]
    public void Explains_when_a_file_is_missing()
    {
        SampleWorkbookBuilder.Source().SaveAs(_folder.File("RSVP.xlsx"));

        var ex = Assert.Throws<InputFileException>(() => InputFileFinder.Find(_folder.Path));
        Assert.Contains("master guest list", ex.Message);
        Assert.Contains("RSVP.xlsx", ex.Message);
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
        Assert.Equal("12", ValueFor("Matched"));
        Assert.Equal("4", ValueFor("Guest not found"));
        Assert.Equal("10", ValueFor("Total"));
        Assert.Equal("1", ValueFor("Warnings"));
    }

    [Fact]
    public void Differences_are_a_filterable_table_with_one_row_each()
    {
        using var workbook = WriteSampleReport();
        var sheet = workbook.Worksheet(ExcelReportWriter.DifferencesSheet);

        var table = sheet.Tables.Single();
        Assert.True(table.ShowAutoFilter);
        Assert.Equal(10, table.DataRange.RowCount());
        Assert.Equal(
            ["Type", "Guest", "Name in Source", "Guest of", "Details", "Possible match", "Matched by", "Source", "Processed",
             "Thu", "Fri", "Sat", "Sun", "Gala", "Source location", "Processed location"],
            table.Fields.Select(f => f.Name));

        IXLTableRow Row(string guest, string type) =>
            table.DataRange.Rows().Single(r => r.Field("Guest").GetString() == guest && r.Field("Type").GetString() == type);

        // Marco's two numbers, with the location of his four Source rows.
        var marco = Row("Marco Bellini", "Mobile mismatch");
        Assert.Equal("Only in Source: 049157156", marco.Field("Details").GetString());
        Assert.Equal("Thurs_Sun Suite RSVP · rows 2, 4, 6, 8 · Order Q7RT2MXA", marco.Field("Source location").GetString());
        Assert.Equal("Sheet1 · row 2", marco.Field("Processed location").GetString());
        Assert.Equal("", marco.Field("Gala").GetString());

        // Ethan isn't on the Grid Gala sheet but is attending in the master list: Gala differs and is highlighted.
        var ethan = Row("Ethan Cole", "Dates mismatch");
        Assert.Equal("– → ✓", ethan.Field("Gala").GetString());
        Assert.Equal(XLColor.FromHtml("#FDE68A"), ethan.Field("Gala").Style.Fill.BackgroundColor);
        Assert.Equal("✗ → ✗", ethan.Field("Sat").GetString());
        Assert.NotEqual(XLColor.FromHtml("#FDE68A"), ethan.Field("Sat").Style.Fill.BackgroundColor);

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
        new CliApp(TestHandlers.CreateCompareFilesHandler(), _output).Run(CliOptions.Parse(args), _folder.Path);

    private string[] Reports() =>
        Directory.GetFiles(_folder.Path, $"{InputFileFinder.ReportFilePrefix}*.xlsx").Select(Path.GetFileName).ToArray()!;

    [Fact]
    public void Writes_a_report_next_to_the_files()
    {
        _folder.AddSamples();

        Assert.Equal(ExitCodes.Success, Run());

        var report = Assert.Single(Reports());
        Assert.Matches(@"^GALA Reconciliation \d{4}-\d{2}-\d{2} \d{4}\.xlsx$", report);
        var text = _output.ToString();
        Assert.Contains("Source file:    RSVP export.xlsx", text);
        Assert.Contains("Processed file: Master list.xlsx", text);
        Assert.Contains("10 differences: 4 guest not found, 2 dates mismatch, 2 mobile mismatch, 2 email mismatch", text);
        Assert.Contains("Report saved:", text);
    }

    [Fact]
    public void Running_again_keeps_the_earlier_report()
    {
        _folder.AddSamples();

        Assert.Equal(ExitCodes.Success, Run());
        Assert.Equal(ExitCodes.Success, Run());

        Assert.Equal(2, Reports().Length);
    }

    [Fact]
    public void Swaps_files_given_the_wrong_way_round()
    {
        _folder.AddSamples();

        Assert.Equal(ExitCodes.Success, Run("Master list.xlsx", "RSVP export.xlsx", "--output", "out.xlsx"));

        Assert.Contains("wrong way round", _output.ToString());
        Assert.True(File.Exists(_folder.File("out.xlsx")));
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
    public void Report_path_never_overwrites_and_accepts_a_folder()
    {
        var runAt = new DateTimeOffset(2026, 11, 9, 14, 30, 0, TimeSpan.Zero);
        var first = CliApp.ReportPath(null, _folder.Path, _folder.Path, runAt);
        File.WriteAllText(first, "existing");

        Assert.Equal(_folder.File("GALA Reconciliation 2026-11-09 1430.xlsx"), first);
        Assert.Equal(_folder.File("GALA Reconciliation 2026-11-09 1430 (2).xlsx"), CliApp.ReportPath(null, _folder.Path, _folder.Path, runAt));
        Assert.Equal(_folder.File("mine.xlsx"), CliApp.ReportPath("mine", _folder.Path, _folder.Path, runAt));
        Assert.Equal(_folder.File("GALA Reconciliation 2026-11-09 1430 (2).xlsx"), CliApp.ReportPath(".", _folder.Path, "C:\\elsewhere", runAt));
    }
}
