using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;
using GalaReconciler.Core.Parsing;
using GalaReconciler.Core.Rules;

namespace GalaReconciler.Core;

/// <param name="GalaFile">
/// The separate Gala RSVP export ("RSVP - Attending" / "RSVP - Not attending"), compared with the master list's
/// Grid Gala column. Without it, Grid Gala comes from the main export's "Grid Gala …" tickets as before.
/// </param>
public sealed record CompareFilesRequest(
    Stream SourceFile,
    string SourceFileName,
    Stream ProcessedFile,
    string ProcessedFileName,
    Stream? GalaFile = null,
    string? GalaFileName = null);

public sealed record ReconciliationResult(
    string SourceFileName,
    string ProcessedFileName,
    DateTimeOffset RunAt,
    TimeSpan Duration,
    IReadOnlyList<Discrepancy> Discrepancies,
    IReadOnlyList<ReconciliationWarning> Warnings,
    int SourceGuestCount,
    int ProcessedGuestCount,
    int MatchedCount,
    string? GalaFileName = null);

/// <summary>Parse both files → match guests → run every rule over each matched pair.</summary>
public sealed class CompareFilesHandler(
    SourceRsvpParser sourceParser,
    ProcessedGuestListParser processedParser,
    GuestMatcher matcher,
    IEnumerable<IDiscrepancyRule> rules,
    TimeProvider timeProvider)
{
    public ReconciliationResult Handle(CompareFilesRequest request)
    {
        var started = timeProvider.GetTimestamp();
        var runAt = timeProvider.GetLocalNow();

        var source = sourceParser.Parse(request.SourceFile, request.GalaFile);
        var processed = processedParser.Parse(request.ProcessedFile);
        var match = matcher.Match(source.Records, processed.Records);

        var items = new List<(Discrepancy Discrepancy, SortKey Sort)>();

        foreach (var pair in match.Matched)
        {
            var sort = SortKey.For(pair.Processed.MatchKey, pair.Processed.HostMatchKey ?? pair.Source.HostMatchKey);
            foreach (var rule in rules)
            {
                if (rule.Evaluate(pair) is { } discrepancy)
                {
                    items.Add((discrepancy, sort));
                }
            }
        }

        // Unmatched records only ever produce GuestNotFound. A "guest" in the master list's Guest Name column
        // (name not known yet) isn't reported when the export has no partner for it.
        items.AddRange(match.OnlyInSource.Select(u => (NotFound(u, inSource: true), SortKey.For(u.Record))));
        items.AddRange(match.OnlyInProcessed
            .Where(u => !u.Record.IsUnnamedGuest)
            .Select(u => (NotFound(u, inSource: false), SortKey.For(u.Record))));

        var ordered = items
            .OrderBy(i => i.Sort.Group, StringComparer.Ordinal)
            .ThenBy(i => i.Sort.IsGuest)
            .ThenBy(i => i.Sort.Name, StringComparer.Ordinal)
            .ThenBy(i => i.Discrepancy.Type)
            .Select(i => i.Discrepancy)
            .ToList();

        return new ReconciliationResult(
            request.SourceFileName,
            request.ProcessedFileName,
            runAt,
            timeProvider.GetElapsedTime(started),
            ordered,
            [.. source.Warnings, .. processed.Warnings],
            source.Records.Count,
            processed.Records.Count,
            match.Matched.Count,
            request.GalaFile is null ? null : request.GalaFileName);
    }

    private static Discrepancy NotFound(UnmatchedGuest unmatched, bool inSource)
    {
        var record = unmatched.Record;
        return new Discrepancy
        {
            Type = DiscrepancyType.GuestNotFound,
            GuestDisplayName = record.DisplayName,
            HostDisplayName = record.HostDisplayName,
            Detail = inSource ? "Only in Source" : "Only in Processed",
            PossibleMatch = unmatched.PossibleMatch,
            SourceValue = inSource ? record.DescribeAttendance() : null,
            ProcessedValue = inSource ? null : record.DescribeAttendance(),
            SourceRows = inSource ? record.Rows : [],
            ProcessedRows = inSource ? [] : record.Rows,
        };
    }

    /// <summary>Orders by name, keeping each guest directly under their host.</summary>
    private sealed record SortKey(string Group, bool IsGuest, string Name)
    {
        public static SortKey For(GuestRecord record) => For(record.MatchKey, record.HostMatchKey);

        public static SortKey For(string matchKey, string? hostMatchKey) =>
            new(hostMatchKey ?? matchKey, hostMatchKey is not null, matchKey);
    }
}
