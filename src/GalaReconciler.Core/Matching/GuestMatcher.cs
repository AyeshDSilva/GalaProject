using GalaReconciler.Core.Domain;

namespace GalaReconciler.Core.Matching;

public enum MatchMethod
{
    Name,
    Email,
}

public sealed record MatchedPair(GuestRecord Source, GuestRecord Processed, MatchMethod MatchedBy)
{
    /// <summary>The master list's spelling wins for display.</summary>
    public string DisplayName => Processed.DisplayName;

    public string? HostDisplayName => Processed.HostDisplayName ?? Source.HostDisplayName;

    /// <summary>The Source spelling, only when it reads differently from the Processed one.</summary>
    public string? SourceDisplayName =>
        string.Equals(Source.DisplayName, Processed.DisplayName, StringComparison.OrdinalIgnoreCase) ? null : Source.DisplayName;
}

public sealed record UnmatchedGuest(GuestRecord Record, string? PossibleMatch);

public sealed record MatchResult(
    IReadOnlyList<MatchedPair> Matched,
    IReadOnlyList<UnmatchedGuest> OnlyInSource,
    IReadOnlyList<UnmatchedGuest> OnlyInProcessed);

/// <summary>
/// Pairs Source and Processed records: by normalized name first, then by a shared email.
/// Phones are never used to match because several people can share one number.
/// </summary>
public sealed class GuestMatcher
{
    public const int PossibleMatchMaxDistance = 2;

    public MatchResult Match(IReadOnlyList<GuestRecord> source, IReadOnlyList<GuestRecord> processed)
    {
        var processedByKey = new Dictionary<string, GuestRecord>(StringComparer.Ordinal);
        foreach (var record in processed)
        {
            processedByKey.TryAdd(record.MatchKey, record);
        }

        var matched = new List<MatchedPair>();
        var matchedProcessed = new HashSet<GuestRecord>(ReferenceEqualityComparer.Instance);
        var unmatchedSource = new List<GuestRecord>();

        // 1. Name.
        foreach (var record in source)
        {
            if (processedByKey.TryGetValue(record.MatchKey, out var candidate) && matchedProcessed.Add(candidate))
            {
                matched.Add(new MatchedPair(record, candidate, MatchMethod.Name));
            }
            else
            {
                unmatchedSource.Add(record);
            }
        }

        var unmatchedProcessed = processed.Where(p => !matchedProcessed.Contains(p)).ToList();

        // 2. Email: only when exactly one record on each side shares the address, so a shared
        // family or office email can't pair the wrong people.
        foreach (var record in unmatchedSource.ToList())
        {
            if (record.Emails.Count == 0)
            {
                continue;
            }

            var candidates = unmatchedProcessed.Where(p => p.Emails.Overlaps(record.Emails)).ToList();
            if (candidates.Count != 1)
            {
                continue;
            }

            var candidate = candidates[0];
            if (unmatchedSource.Count(s => s.Emails.Overlaps(candidate.Emails)) != 1)
            {
                continue;
            }

            matched.Add(new MatchedPair(record, candidate, MatchMethod.Email));
            unmatchedSource.Remove(record);
            unmatchedProcessed.Remove(candidate);
        }

        // 3. Whatever's left is not found; suggest a similar name from the other side, but never match it.
        return new MatchResult(
            matched,
            unmatchedSource.Select(r => new UnmatchedGuest(r, FindPossibleMatch(r, unmatchedProcessed))).ToList(),
            unmatchedProcessed.Select(r => new UnmatchedGuest(r, FindPossibleMatch(r, unmatchedSource))).ToList());
    }

    private static string? FindPossibleMatch(GuestRecord record, IEnumerable<GuestRecord> candidates) =>
        candidates
            .Select(c => (c.DisplayName, Distance: Levenshtein.Distance(record.MatchKey, c.MatchKey)))
            .Where(c => c.Distance <= PossibleMatchMaxDistance)
            .OrderBy(c => c.Distance)
            .Select(c => c.DisplayName)
            .FirstOrDefault();
}
