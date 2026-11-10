using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;

namespace GalaReconciler.Web.Features.Reconciliation.Persistence;

public enum RunStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
}

/// <summary>
/// One comparison. Only the results are stored; the uploaded files are held in memory until the
/// background worker has processed them and are then discarded.
/// </summary>
public sealed class Run
{
    public Guid Id { get; set; }

    /// <summary>UTC. Stored as DateTime because SQLite can't sort or compare DateTimeOffset.</summary>
    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public RunStatus Status { get; set; }

    public string SourceFileName { get; set; } = "";

    public long SourceFileSize { get; set; }

    public string ProcessedFileName { get; set; } = "";

    public long ProcessedFileSize { get; set; }

    public double? DurationMs { get; set; }

    public int SourceGuestCount { get; set; }

    public int ProcessedGuestCount { get; set; }

    public int MatchedCount { get; set; }

    // Per-type counts are denormalized so the runs list can show and filter them without loading discrepancies.
    public int GuestNotFoundCount { get; set; }

    public int DatesMismatchCount { get; set; }

    public int MobileNumberMismatchCount { get; set; }

    public int EmailMismatchCount { get; set; }

    public int TitleMismatchCount { get; set; }

    public int PostNominalMismatchCount { get; set; }

    public int PositionMismatchCount { get; set; }

    public int WarningCount { get; set; }

    public string? ErrorTitle { get; set; }

    public string? ErrorMessage { get; set; }

    public List<string> MissingHeaders { get; set; } = [];

    public List<RunDiscrepancy> Discrepancies { get; set; } = [];

    public List<RunWarning> Warnings { get; set; } = [];

    public int DiscrepancyCount => ReconciliationResultViewModel.Types.Sum(CountOf);

    public int CountOf(DiscrepancyType type) => type switch
    {
        DiscrepancyType.GuestNotFound => GuestNotFoundCount,
        DiscrepancyType.DatesMismatch => DatesMismatchCount,
        DiscrepancyType.MobileNumberMismatch => MobileNumberMismatchCount,
        DiscrepancyType.EmailMismatch => EmailMismatchCount,
        DiscrepancyType.TitleMismatch => TitleMismatchCount,
        DiscrepancyType.PostNominalMismatch => PostNominalMismatchCount,
        DiscrepancyType.PositionMismatch => PositionMismatchCount,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Add a count column for this type."),
    };
}

public sealed class RunDiscrepancy
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    /// <summary>Order on the results page.</summary>
    public int Position { get; set; }

    public DiscrepancyType Type { get; set; }

    public string GuestDisplayName { get; set; } = "";

    public string? HostDisplayName { get; set; }

    public string? SourceDisplayName { get; set; }

    public MatchMethod? MatchedBy { get; set; }

    public string Detail { get; set; } = "";

    public string? PossibleMatch { get; set; }

    public string? SourceValue { get; set; }

    public string? ProcessedValue { get; set; }

    /// <summary>JSON array of <see cref="DayComparison"/>.</summary>
    public string DaysJson { get; set; } = "[]";

    /// <summary>JSON array of <see cref="RowReference"/>.</summary>
    public string SourceRowsJson { get; set; } = "[]";

    /// <summary>JSON array of <see cref="RowReference"/>.</summary>
    public string ProcessedRowsJson { get; set; } = "[]";
}

public sealed class RunWarning
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    public int Position { get; set; }

    public string File { get; set; } = "";

    public string Message { get; set; } = "";
}
