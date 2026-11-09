namespace GalaReconciler.Cli;

/// <summary>Parsed command line. Everything is optional: with no arguments the app searches the current folder.</summary>
public sealed record CliOptions
{
    public const string Usage = """
        Usage: GalaReconciler [source.xlsx processed.xlsx] [options]

        Compares the RSVP export (Source) with the master guest list (Processed) and writes
        the differences to an Excel report.

        With no file names, the folder is searched for the two workbooks and they are told
        apart by their column headers, so the files can be called anything.

        Options:
          --folder <folder>   Folder to search for the two files (default: the current folder)
          --output <file>     Where to save the report (default: "GALA Reconciliation <date> <time>.xlsx"
                              in the folder). Can also be a folder.
          --strict            Treat "not attending" and "no response" as different
          --no-pause          Don't wait for a key press before closing
          -h, --help          Show this help
        """;

    public string? SourceFile { get; init; }

    public string? ProcessedFile { get; init; }

    public string? Folder { get; init; }

    public string? Output { get; init; }

    public bool StrictNotAttending { get; init; }

    public bool NoPause { get; init; }

    public bool ShowHelp { get; init; }

    /// <summary>Set when the arguments couldn't be understood.</summary>
    public string? Error { get; init; }

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        var files = new List<string>();
        string? folder = null, output = null;
        bool strict = false, noPause = false, help = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "-h" or "--help" or "/?":
                    help = true;
                    break;
                case "--strict":
                    strict = true;
                    break;
                case "--no-pause":
                    noPause = true;
                    break;
                case "--folder" or "--output":
                    if (i + 1 >= args.Count)
                    {
                        return new CliOptions { Error = $"{arg} needs a value." };
                    }

                    if (arg.Equals("--folder", StringComparison.OrdinalIgnoreCase))
                    {
                        folder = args[++i];
                    }
                    else
                    {
                        output = args[++i];
                    }

                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        return new CliOptions { Error = $"Unknown option {arg}." };
                    }

                    files.Add(arg);
                    break;
            }
        }

        if (files.Count is not (0 or 2))
        {
            return new CliOptions { Error = "Give both file names (Source then Processed), or none to search the folder." };
        }

        return new CliOptions
        {
            SourceFile = files.Count == 2 ? files[0] : null,
            ProcessedFile = files.Count == 2 ? files[1] : null,
            Folder = folder,
            Output = output,
            StrictNotAttending = strict,
            NoPause = noPause,
            ShowHelp = help,
        };
    }
}
