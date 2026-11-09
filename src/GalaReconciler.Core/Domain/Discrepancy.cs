using GalaReconciler.Core.Matching;

namespace GalaReconciler.Core.Domain;

public sealed record DayComparison(EventDay Day, AttendanceState Source, AttendanceState Processed, bool IsDifferent);

public sealed record Discrepancy
{
    public required DiscrepancyType Type { get; init; }

    public required string GuestDisplayName { get; init; }

    public string? HostDisplayName { get; init; }

    /// <summary>The guest's name as written in the Source file, when it differs from the Processed name.</summary>
    public string? SourceDisplayName { get; init; }

    public MatchMethod? MatchedBy { get; init; }

    /// <summary>Every event day with both states (DatesMismatch only).</summary>
    public IReadOnlyList<DayComparison> Days { get; init; } = [];

    public IEnumerable<EventDay> AffectedDays => Days.Where(d => d.IsDifferent).Select(d => d.Day);

    /// <summary>Newline-separated values from the Source file.</summary>
    public string? SourceValue { get; init; }

    /// <summary>Newline-separated values from the Processed file.</summary>
    public string? ProcessedValue { get; init; }

    public IReadOnlyList<RowReference> SourceRows { get; init; } = [];

    public IReadOnlyList<RowReference> ProcessedRows { get; init; } = [];

    public required string Detail { get; init; }

    /// <summary>A similarly named record on the other side (GuestNotFound only). Never auto-matched.</summary>
    public string? PossibleMatch { get; init; }
}
