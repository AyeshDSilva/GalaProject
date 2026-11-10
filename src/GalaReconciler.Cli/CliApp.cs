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

/// <summary>Finds the three files, compares them and saves the Excel report.</summary>
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

            var inputs = options.Files.Count > 0
                ? ReadNamedFiles(options.Files.Select(f => Path.GetFullPath(f, currentDirectory)).ToList())
                : FindInFolder(folder);

            output.WriteLine($"  Source file:    {inputs.Source.Name}");
            output.WriteLine($"  Gala file:      {inputs.Gala.Name}");
            output.WriteLine($"  Processed file: {inputs.Processed.Name}");
            output.WriteLine();
            output.WriteLine("Comparing...");

            using var source = new MemoryStream(inputs.Source.Contents, writable: false);
            using var gala = new MemoryStream(inputs.Gala.Contents, writable: false);
            using var processed = new MemoryStream(inputs.Processed.Contents, writable: false);
            var result = handler.Handle(new CompareFilesRequest(
                source, inputs.Source.Name, processed, inputs.Processed.Name, gala, inputs.Gala.Name));

            // Two new files; the originals are only ever read.
            var paths = OutputPaths.For(options.Output, currentDirectory, folder, result.RunAt, inputs.Processed.Name);
            int highlightedCells;
            try
            {
                highlightedCells = ProcessedFileHighlighter.Write(inputs.Processed.Contents, result, paths.HighlightedCopy);
                ExcelReportWriter.Write(result, paths.Results, Path.GetFileName(paths.HighlightedCopy));
            }
            catch (IOException ex)
            {
                throw new InputFileException(
                    $"Couldn't save the output files in {Path.GetDirectoryName(paths.Results)}.\n" +
                    $"If a file with the same name is open in Excel, close it and try again. ({ex.Message})");
            }

            WriteSummary(result, paths, highlightedCells);
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


    private InputFiles FindInFolder(string folder)
    {
        output.WriteLine($"Looking for the three files in {folder}");
        return InputFileFinder.Find(folder);
    }

    /// <summary>The three files can be given in any order; each is recognised by its contents.</summary>
    private static InputFiles ReadNamedFiles(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                throw new InputFileException($"File not found: {path}");
            }
        }

        var detected = paths.Select(InputFile.Read).Select(file => (File: file, Kind: file.DetectKind())).ToList();

        InputFile One(WorkbookKind kind, string what, string recognisedBy)
        {
            var files = detected.Where(d => d.Kind == kind).Select(d => d.File).ToList();
            return files.Count == 1
                ? files[0]
                : throw new InputFileException(
                    (files.Count == 0
                        ? $"None of the files given is the {what}."
                        : $"More than one of the files given looks like the {what}: {string.Join(", ", files.Select(f => f.Name))}.") +
                    $"\nIt's recognised by its {recognisedBy}.");
        }

        return new InputFiles(
            One(WorkbookKind.Source, InputFiles.SourceDescription, InputFiles.SourceRecognisedBy),
            One(WorkbookKind.GalaSource, InputFiles.GalaDescription, InputFiles.GalaRecognisedBy),
            One(WorkbookKind.Processed, InputFiles.ProcessedDescription, InputFiles.ProcessedRecognisedBy));
    }

    private void WriteSummary(ReconciliationResult result, OutputPaths paths, int highlightedCells)
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
        output.WriteLine($"Results saved:           {paths.Results}");
        output.WriteLine($"Highlighted master list: {paths.HighlightedCopy}");
        output.WriteLine($"  ({highlightedCells} {(highlightedCells == 1 ? "cell" : "cells")} highlighted; the original files were not changed)");
    }
}

/// <summary>Where the results workbook and the highlighted copy of the master list are saved.</summary>
public sealed record OutputPaths(string Results, string HighlightedCopy)
{
    public const string HighlightedSuffix = "(highlighted)";

    /// <summary>
    /// Default "Guest List Reconciliation 2026-11-09 1430.xlsx" and "Guest List Reconciliation 2026-11-09 1430 - Master list
    /// (highlighted).xlsx" in the folder, numbered "(2)", "(3)"… rather than overwriting earlier runs.
    /// </summary>
    /// <param name="requested">--output: the results file, or a folder to put both files in.</param>
    public static OutputPaths For(string? requested, string currentDirectory, string folder, DateTimeOffset runAt, string processedFileName)
    {
        var processedName = Path.GetFileNameWithoutExtension(processedFileName);
        OutputPaths Named(string directory, string stem) =>
            new(Path.Combine(directory, $"{stem}.xlsx"), Path.Combine(directory, $"{stem} - {processedName} {HighlightedSuffix}.xlsx"));

        if (requested is not null)
        {
            var full = Path.GetFullPath(requested, currentDirectory);
            if (!Directory.Exists(full))
            {
                // An explicit file name is used as given.
                return Named(Path.GetDirectoryName(full)!, Path.HasExtension(full) ? Path.GetFileNameWithoutExtension(full) : Path.GetFileName(full));
            }

            folder = full;
        }

        var stem = $"{InputFileFinder.ReportFilePrefix} {runAt:yyyy-MM-dd HHmm}";
        var paths = Named(folder, stem);
        for (var copy = 2; File.Exists(paths.Results) || File.Exists(paths.HighlightedCopy); copy++)
        {
            paths = Named(folder, $"{stem} ({copy})");
        }

        return paths;
    }
}
