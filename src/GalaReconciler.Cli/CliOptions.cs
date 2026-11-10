namespace GalaReconciler.Cli;

/// <summary>Parsed command line. Everything is optional: with no arguments the app searches the current folder.</summary>
public sealed record CliOptions
{
    public const string Usage = """
        Usage: GalaReconciler [source.xlsx gala.xlsx processed.xlsx] [options]

        Compares the RSVP export (Source) and the Gala RSVP export with the master guest list
        (Processed). Saves the differences to a results workbook and a highlighted copy of the
        master list; the original files are never changed.

        With no file names, the folder is searched for the three workbooks and they are told
        apart by their columns and ticket types, so the files can be called anything (and given
        in any order).

        Options:
          --folder <folder>   Folder to search for the three files (default: the current folder)
          --output <file>     Where to save the results (default: "GALA Reconciliation <date> <time>.xlsx"
                              in the folder). The highlighted copy goes next to it. Can also be a folder.
          --strict            Also flag days marked N in the master list that have no ticket in the export
          --no-pause          Don't wait for a key press before closing
          -h, --help          Show this help
        """;

    /// <summary>The three file names given on the command line, in any order (empty to search the folder).</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

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

        if (files.Count is not (0 or 3))
        {
            return new CliOptions { Error = "Give all three file names (RSVP export, Gala RSVP export and master list, in any order), or none to search the folder." };
        }

        return new CliOptions
        {
            Files = files,
            Folder = folder,
            Output = output,
            StrictNotAttending = strict,
            NoPause = noPause,
            ShowHelp = help,
        };
    }
}
