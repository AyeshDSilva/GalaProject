using System.Text.Json;
using System.Text.Json.Serialization;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core;
using Microsoft.EntityFrameworkCore;

namespace GalaReconciler.Web.Features.Reconciliation.Persistence;

/// <summary>Saves and loads comparison runs.</summary>
public sealed class RunStore(ReconciliationDbContext db, TimeProvider timeProvider)
{
    public const int PageSize = 25;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<Guid> CreateQueuedAsync(
        string sourceFileName,
        long sourceFileSize,
        string processedFileName,
        long processedFileSize,
        CancellationToken cancellationToken)
    {
        var run = new Run
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = UtcNow(),
            Status = RunStatus.Queued,
            SourceFileName = sourceFileName,
            SourceFileSize = sourceFileSize,
            ProcessedFileName = processedFileName,
            ProcessedFileSize = processedFileSize,
        };
        db.Runs.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    public Task MarkProcessingAsync(Guid id, CancellationToken cancellationToken) =>
        db.Runs.Where(r => r.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, RunStatus.Processing), cancellationToken);

    public async Task CompleteAsync(Guid id, ReconciliationResult result, CancellationToken cancellationToken)
    {
        var run = await db.Runs.SingleAsync(r => r.Id == id, cancellationToken);

        run.Status = RunStatus.Completed;
        run.CompletedAtUtc = UtcNow();
        run.DurationMs = result.Duration.TotalMilliseconds;
        run.SourceGuestCount = result.SourceGuestCount;
        run.ProcessedGuestCount = result.ProcessedGuestCount;
        run.MatchedCount = result.MatchedCount;
        run.GuestNotFoundCount = result.Discrepancies.Count(d => d.Type == DiscrepancyType.GuestNotFound);
        run.DatesMismatchCount = result.Discrepancies.Count(d => d.Type == DiscrepancyType.DatesMismatch);
        run.MobileNumberMismatchCount = result.Discrepancies.Count(d => d.Type == DiscrepancyType.MobileNumberMismatch);
        run.EmailMismatchCount = result.Discrepancies.Count(d => d.Type == DiscrepancyType.EmailMismatch);
        run.WarningCount = result.Warnings.Count;

        run.Discrepancies.AddRange(result.Discrepancies.Select((d, index) => new RunDiscrepancy
        {
            Position = index,
            Type = d.Type,
            GuestDisplayName = d.GuestDisplayName,
            HostDisplayName = d.HostDisplayName,
            SourceDisplayName = d.SourceDisplayName,
            MatchedBy = d.MatchedBy,
            Detail = d.Detail,
            PossibleMatch = d.PossibleMatch,
            SourceValue = d.SourceValue,
            ProcessedValue = d.ProcessedValue,
            DaysJson = JsonSerializer.Serialize(d.Days, Json),
            SourceRowsJson = JsonSerializer.Serialize(d.SourceRows, Json),
            ProcessedRowsJson = JsonSerializer.Serialize(d.ProcessedRows, Json),
        }));
        run.Warnings.AddRange(result.Warnings.Select((w, index) => new RunWarning
        {
            Position = index,
            File = w.File,
            Message = w.Message,
        }));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid id, string title, string message, IReadOnlyList<string> missingHeaders, CancellationToken cancellationToken)
    {
        // Drop anything left tracked by a CompleteAsync that failed part-way, so it isn't saved too.
        db.ChangeTracker.Clear();
        var run = await db.Runs.SingleAsync(r => r.Id == id, cancellationToken);
        run.Status = RunStatus.Failed;
        run.CompletedAtUtc = UtcNow();
        run.ErrorTitle = title;
        run.ErrorMessage = message;
        run.MissingHeaders = missingHeaders.ToList();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The run with its differences and warnings in display order, or null.</summary>
    public Task<Run?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Runs
            .AsNoTracking()
            .Include(r => r.Discrepancies.OrderBy(d => d.Position))
            .Include(r => r.Warnings.OrderBy(w => w.Position))
            .AsSplitQuery()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<RunStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken) =>
        db.Runs.Where(r => r.Id == id).Select(r => (RunStatus?)r.Status).SingleOrDefaultAsync(cancellationToken);

    public async Task<RunsViewModel> ListAsync(RunFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Runs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.File))
        {
            var pattern = LikePattern(filter.File);
            query = query.Where(r => EF.Functions.Like(r.SourceFileName, pattern, "\\") || EF.Functions.Like(r.ProcessedFileName, pattern, "\\"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Guest))
        {
            var pattern = LikePattern(filter.Guest);
            query = query.Where(r => r.Discrepancies.Any(d =>
                EF.Functions.Like(d.GuestDisplayName, pattern, "\\")
                || EF.Functions.Like(d.SourceDisplayName!, pattern, "\\")
                || EF.Functions.Like(d.HostDisplayName!, pattern, "\\")));
        }

        if (filter.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        query = filter.Type switch
        {
            DiscrepancyType.GuestNotFound => query.Where(r => r.GuestNotFoundCount > 0),
            DiscrepancyType.DatesMismatch => query.Where(r => r.DatesMismatchCount > 0),
            DiscrepancyType.MobileNumberMismatch => query.Where(r => r.MobileNumberMismatchCount > 0),
            DiscrepancyType.EmailMismatch => query.Where(r => r.EmailMismatchCount > 0),
            _ => query,
        };

        // Dates in the filter are the user's local calendar days.
        if (filter.From is { } from)
        {
            var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(r => r.CreatedAtUtc >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(r => r.CreatedAtUtc < toUtc);
        }

        var total = await query.CountAsync(cancellationToken);
        var page = Math.Clamp(filter.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)PageSize)));
        var runs = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        return new RunsViewModel(filter, runs, total, PageSize);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Runs.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Deletes runs (and, by cascade, their results) created before <paramref name="cutoffUtc"/>.</summary>
    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        db.Runs.Where(r => r.CreatedAtUtc < cutoffUtc).ExecuteDeleteAsync(cancellationToken);

    /// <summary>
    /// Runs left queued or processing by a restart can never finish: their files were only in memory.
    /// </summary>
    public Task<int> FailInterruptedAsync(CancellationToken cancellationToken) =>
        db.Runs.Where(r => r.Status == RunStatus.Queued || r.Status == RunStatus.Processing).ExecuteUpdateAsync(
            set => set
                .SetProperty(r => r.Status, RunStatus.Failed)
                .SetProperty(r => r.CompletedAtUtc, UtcNow())
                .SetProperty(r => r.ErrorTitle, "This comparison was interrupted")
                .SetProperty(r => r.ErrorMessage, "The server restarted before the files were processed. Upload them again to compare."),
            cancellationToken);

    /// <summary>Rebuilds the comparison result from a completed run, for the results page.</summary>
    public static ReconciliationResult ToResult(Run run) => new(
        run.SourceFileName,
        run.ProcessedFileName,
        new DateTimeOffset(DateTime.SpecifyKind(run.CreatedAtUtc, DateTimeKind.Utc)).ToLocalTime(),
        TimeSpan.FromMilliseconds(run.DurationMs ?? 0),
        run.Discrepancies.OrderBy(d => d.Position).Select(ToDiscrepancy).ToList(),
        run.Warnings.OrderBy(w => w.Position).Select(w => new ReconciliationWarning(w.File, w.Message)).ToList(),
        run.SourceGuestCount,
        run.ProcessedGuestCount,
        run.MatchedCount);

    private static Discrepancy ToDiscrepancy(RunDiscrepancy d) => new()
    {
        Type = d.Type,
        GuestDisplayName = d.GuestDisplayName,
        HostDisplayName = d.HostDisplayName,
        SourceDisplayName = d.SourceDisplayName,
        MatchedBy = d.MatchedBy,
        Detail = d.Detail,
        PossibleMatch = d.PossibleMatch,
        SourceValue = d.SourceValue,
        ProcessedValue = d.ProcessedValue,
        Days = JsonSerializer.Deserialize<List<DayComparison>>(d.DaysJson, Json) ?? [],
        SourceRows = JsonSerializer.Deserialize<List<RowReference>>(d.SourceRowsJson, Json) ?? [],
        ProcessedRows = JsonSerializer.Deserialize<List<RowReference>>(d.ProcessedRowsJson, Json) ?? [],
    };

    private static string LikePattern(string text) =>
        "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
