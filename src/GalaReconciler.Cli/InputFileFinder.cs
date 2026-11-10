using GalaReconciler.Core.Parsing;

namespace GalaReconciler.Cli;

public sealed record InputFile(string Path, byte[] Contents)
{
    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>Reads the whole file, even while it's open in Excel.</summary>
    public static InputFile Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var contents = new byte[stream.Length];
        stream.ReadExactly(contents);
        return new InputFile(System.IO.Path.GetFullPath(path), contents);
    }

    public WorkbookKind DetectKind()
    {
        using var stream = new MemoryStream(Contents, writable: false);
        return WorkbookDetector.Detect(stream);
    }
}

/// <param name="Source">The main RSVP export (Thursday to Sunday).</param>
/// <param name="Gala">The Gala RSVP export ("RSVP - Attending" / "RSVP - Not attending").</param>
/// <param name="Processed">The master guest list.</param>
public sealed record InputFiles(InputFile Source, InputFile Gala, InputFile Processed)
{
    public const string SourceDescription = "RSVP export (Source file)";
    public const string GalaDescription = "Gala RSVP export";
    public const string ProcessedDescription = "master guest list (Processed file)";

    public const string SourceRecognisedBy = "'Ticket type' and 'Order id' columns, with day ticket types such as 'Saturday attending'";
    public const string GalaRecognisedBy = "'Ticket type' and 'Order id' columns, with the ticket types 'RSVP - Attending' and 'RSVP - Not attending'";
    public const string ProcessedRecognisedBy = "'Surname', 'Guest Name' and day columns (THURS, FRI, …) on its first sheet";
}

/// <summary>A problem finding or reading the input files. The message is written for the user.</summary>
public sealed class InputFileException(string message) : Exception(message);

/// <summary>Finds the RSVP export, the Gala RSVP export and the master guest list in a folder by looking at their contents.</summary>
public static class InputFileFinder
{
    /// <summary>Files this app writes start with this; they (and highlighted copies) are skipped when searching.</summary>
    public const string ReportFilePrefix = "GALA Reconciliation";

    public static InputFiles Find(string folder)
    {
        var candidates = Directory.EnumerateFiles(folder, "*.xlsx")
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                // "~$…" files are Excel's lock files for workbooks that are open.
                return !name.StartsWith("~$", StringComparison.Ordinal)
                    && !name.StartsWith(ReportFilePrefix, StringComparison.OrdinalIgnoreCase)
                    && !Path.GetFileNameWithoutExtension(name).EndsWith(OutputPaths.HighlightedSuffix, StringComparison.OrdinalIgnoreCase);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InputFileException(
                $"No .xlsx files found in {folder}.\nPut the RSVP export, the Gala RSVP export and the master guest list in this folder and run again.");
        }

        var detected = candidates.Select(InputFile.Read).Select(file => (File: file, Kind: file.DetectKind())).ToList();
        List<InputFile> Of(WorkbookKind kind) => detected.Where(d => d.Kind == kind).Select(d => d.File).ToList();
        var looked = string.Join(", ", detected.Select(d => d.File.Name));

        return new InputFiles(
            Single(Of(WorkbookKind.Source), InputFiles.SourceDescription, InputFiles.SourceRecognisedBy, folder, looked),
            Single(Of(WorkbookKind.GalaSource), InputFiles.GalaDescription, InputFiles.GalaRecognisedBy, folder, looked),
            Single(Of(WorkbookKind.Processed), InputFiles.ProcessedDescription, InputFiles.ProcessedRecognisedBy, folder, looked));
    }

    private static InputFile Single(List<InputFile> files, string what, string recognisedBy, string folder, string looked) =>
        files.Count switch
        {
            1 => files[0],
            0 => throw new InputFileException(
                $"Couldn't find the {what} in {folder}.\nLooked at: {looked}.\nIt's recognised by its {recognisedBy}."),
            _ => throw new InputFileException(
                $"Found more than one {what}: {string.Join(", ", files.Select(f => f.Name))}.\n" +
                "Move the ones you don't want out of the folder, or give the three file names on the command line."),
        };
}
