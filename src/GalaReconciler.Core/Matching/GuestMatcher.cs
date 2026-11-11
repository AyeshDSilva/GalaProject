using GalaReconciler.Core.Domain;

namespace GalaReconciler.Core.Matching;

public enum MatchMethod
{
    Name,
    Email,

    /// <summary>An extra ticket under the host's name, paired with a guest the master list names for that host.</summary>
    GuestOfHost,
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
/// Pairs Source and Processed records: by normalized name, then by another name the export has for the person,
/// then by a shared email, then an extra ticket with a guest named for its host.
/// Phones are never used to match because several people can share one number.
/// </summary>
public sealed class GuestMatcher
{
    public const int PossibleMatchMaxDistance = 2;

    /// <summary>
    /// Whether two normalized names look like one person spelled differently: a couple of letters apart
    /// ("bill hartley" / "will hartley"), or the same surname with one first name shortening the other
    /// ("will hartley" / "william hartley").
    /// </summary>
    public static bool IsSimilarName(string first, string second)
    {
        if (Levenshtein.Distance(first, second) <= PossibleMatchMaxDistance)
        {
            return true;
        }

        var a = first.Split(' ');
        var b = second.Split(' ');
        var (shorter, longer) = a[0].Length <= b[0].Length ? (a[0], b[0]) : (b[0], a[0]);
        return a.Length > 1 && b.Length > 1 && a[^1] == b[^1] && shorter.Length >= 3
            && longer.StartsWith(shorter, StringComparison.Ordinal);
    }

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

        // 2. Another name the export has for the person. After step 1, so an exact name always wins.
        foreach (var record in unmatchedSource.ToList())
        {
            foreach (var key in record.OtherMatchKeys)
            {
                if (processedByKey.TryGetValue(key, out var candidate) && matchedProcessed.Add(candidate))
                {
                    matched.Add(new MatchedPair(record, candidate, MatchMethod.Name));
                    unmatchedSource.Remove(record);
                    break;
                }
            }
        }

        var unmatchedProcessed = processed.Where(p => !matchedProcessed.Contains(p)).ToList();

        // 3. Email: only when exactly one record on each side shares the address, so a shared
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

        // 4. An extra ticket has only the host's name on it. If the master list names a guest for that host who
        // has no ticket of their own, that's who the ticket is for.
        var processedHostOf = matched.ToDictionary(p => p.Source.MatchKey, p => p.Processed.MatchKey, StringComparer.Ordinal);
        foreach (var record in unmatchedSource.Where(r => r.IsUnnamedGuest && r.HostMatchKey is not null).ToList())
        {
            if (!processedHostOf.TryGetValue(record.HostMatchKey!, out var hostKey))
            {
                continue;
            }

            var guest = unmatchedProcessed.FirstOrDefault(p => !p.IsUnnamedGuest && p.HostMatchKey == hostKey);
            if (guest is not null)
            {
                matched.Add(new MatchedPair(record, guest, MatchMethod.GuestOfHost));
                unmatchedSource.Remove(record);
                unmatchedProcessed.Remove(guest);
            }
        }

        // 5. Whatever's left is not found; suggest a similar name from the other side, but never match it.
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
