using GalaReconciler.Core;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Parsing;

namespace GalaReconciler.Cli;

public static class ExitCodes
{
    public const int Success = 0;
    public const int InvalidInput = 1;
    public const int UnexpectedError = 2;
}

/// <summary>Finds the two files, compares them and saves the Excel report.</summary>
public sealed class CliApp(CompareFilesHandler handler, TextWriter output)
{
    public int Run(CliOptions options, string currentDirectory)
    {
        if (options.Error is { } error)
        {
            output.WriteLine(error);
            output.WriteLine("Run with --help to see how to use it.");
            return ExitCodes.InvalidInput;
        }

        if (options.ShowHelp)
        {
            output.WriteLine(CliOptions.Usage);
            return ExitCodes.Success;
        }

        try
        {
            var folder = Path.GetFullPath(options.Folder ?? ".", currentDirectory);
            if (!Directory.Exists(folder))
            {
                throw new InputFileException($"The folder {folder} doesn't exist.");
            }

            output.WriteLine("GALA guest list reconciliation");
            output.WriteLine();

            var inputs = options.SourceFile is not null
                ? ReadNamedFiles(Path.GetFullPath(options.SourceFile, currentDirectory), Path.GetFullPath(options.ProcessedFile!, currentDirectory))
                : FindInFolder(folder);

            output.WriteLine($"  Source file:    {inputs.Source.Name}");
            output.WriteLine($"  Processed file: {inputs.Processed.Name}");
            output.WriteLine();
            output.WriteLine("Comparing...");

            using var source = new MemoryStream(inputs.Source.Contents, writable: false);
            using var processed = new MemoryStream(inputs.Processed.Contents, writable: false);
            var result = handler.Handle(new CompareFilesRequest(source, inputs.Source.Name, processed, inputs.Processed.Name));

            var reportPath = ReportPath(options.Output, currentDirectory, folder, result.RunAt);
            try
            {
                ExcelReportWriter.Write(result, reportPath);
            }
            catch (IOException ex)
            {
                throw new InputFileException(
                    $"Couldn't save the report to {reportPath}.\nIf a file with that name is open in Excel, close it and try again. ({ex.Message})");
            }

            WriteSummary(result, reportPath);
            return ExitCodes.Success;
        }
        catch (InputFileException ex)
        {
            output.WriteLine();
            output.WriteLine(ex.Message);
            return ExitCodes.InvalidInput;
        }
        catch (WorkbookFormatException ex)
        {
            output.WriteLine();
            output.WriteLine(ex.Message);
            if (ex.MissingHeaders.Count > 0)
            {
                output.WriteLine($"Missing: {string.Join(", ", ex.MissingHeaders)}");
            }

            return ExitCodes.InvalidInput;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            output.WriteLine();
            output.WriteLine($"Something went wrong: {ex.Message}");
            return ExitCodes.UnexpectedError;
        }
    }

    /// <summary>Default "GALA Reconciliation 2026-11-09 1430.xlsx", never overwriting an existing report.</summary>
    public static string ReportPath(string? requested, string currentDirectory, string folder, DateTimeOffset runAt)
    {
        var defaultName = $"{InputFileFinder.ReportFilePrefix} {runAt:yyyy-MM-dd HHmm}.xlsx";

        if (requested is not null)
        {
            var full = Path.GetFullPath(requested, currentDirectory);
            if (!Directory.Exists(full))
            {
                return Path.HasExtension(full) ? full : full + ".xlsx";
            }

            folder = full;
        }

        var path = Path.Combine(folder, defaultName);
        for (var copy = 2; File.Exists(path); copy++)
        {
            path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(defaultName)} ({copy}).xlsx");
        }

        return path;
    }

    private InputFiles FindInFolder(string folder)
    {
        output.WriteLine($"Looking for the two files in {folder}");
        return InputFileFinder.Find(folder);
    }

    private InputFiles ReadNamedFiles(string sourcePath, string processedPath)
    {
        foreach (var path in new[] { sourcePath, processedPath })
        {
            if (!File.Exists(path))
            {
                throw new InputFileException($"File not found: {path}");
            }
        }

        var source = InputFile.Read(sourcePath);
        var processed = InputFile.Read(processedPath);

        // Given the wrong way round? Swap rather than fail with confusing missing-column errors.
        if (source.DetectKind() == WorkbookKind.Processed && processed.DetectKind() == WorkbookKind.Source)
        {
            output.WriteLine("The files look like they were given the wrong way round, so they've been swapped.");
            (source, processed) = (processed, source);
        }

        return new InputFiles(source, processed);
    }

    private void WriteSummary(ReconciliationResult result, string reportPath)
    {
        output.WriteLine();
        output.WriteLine($"  Guests: {result.SourceGuestCount} in Source, {result.ProcessedGuestCount} in Processed, {result.MatchedCount} matched");

        var counts = Enum.GetValues<DiscrepancyType>()
            .Select(type => (Type: type, Count: result.Discrepancies.Count(d => d.Type == type)))
            .Where(c => c.Count > 0)
            .Select(c => $"{c.Count} {c.Type.ToDisplay().ToLowerInvariant()}")
            .ToList();
        output.WriteLine(result.Discrepancies.Count == 0
            ? "  No differences found."
            : $"  {result.Discrepancies.Count} differences: {string.Join(", ", counts)}");

        if (result.Warnings.Count > 0)
        {
            output.WriteLine($"  {result.Warnings.Count} {(result.Warnings.Count == 1 ? "warning" : "warnings")} (see the Warnings sheet)");
        }

        output.WriteLine();
        output.WriteLine($"Report saved: {reportPath}");
    }
}
