using GalaReconciler.Core;
using GalaReconciler.Core.Domain;
using GalaReconciler.Tests.Helpers;
using GalaReconciler.Web.Features.Reconciliation;
using GalaReconciler.Web.Features.Reconciliation.Background;
using GalaReconciler.Web.Features.Reconciliation.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GalaReconciler.Tests.Persistence;

public sealed class RunStoreTests : IDisposable
{
    private static readonly ReconciliationResult SampleResult = TestHandlers.CompareSamples();

    private readonly TestDatabase _database = new();
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 9, 2, 0, 0, TimeSpan.Zero));

    public void Dispose() => _database.Dispose();

    private RunStore Store() => new(_database.CreateContext(), _clock);

    private async Task<Guid> CreateAsync(string sourceName = "Source.xlsx") =>
        await Store().CreateQueuedAsync(sourceName, 100, "Processed.xlsx", 200, CancellationToken.None);

    private async Task<Guid> CreateCompletedAsync(string sourceName = "Source.xlsx")
    {
        var id = await CreateAsync(sourceName);
        await Store().CompleteAsync(id, SampleResult, CancellationToken.None);
        return id;
    }

    [Fact]
    public async Task Completed_run_round_trips_to_the_same_result()
    {
        var id = await CreateCompletedAsync();

        var run = await Store().GetAsync(id, CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(4, run.GuestNotFoundCount);
        Assert.Equal(3, run.DatesMismatchCount);
        Assert.Equal(1, run.TitleMismatchCount);
        Assert.Equal(1, run.PostNominalMismatchCount);
        Assert.Equal(1, run.PositionMismatchCount);
        Assert.Equal(13, run.DiscrepancyCount);
        Assert.Equal(1, run.MobileNumberMismatchCount);
        Assert.Equal(2, run.EmailMismatchCount);
        Assert.Equal(1, run.WarningCount);

        var loaded = RunStore.ToResult(run);
        Assert.Equal(SampleResult.MatchedCount, loaded.MatchedCount);
        Assert.Equal(SampleResult.Warnings, loaded.Warnings);
        Assert.Equal(SampleResult.Discrepancies.Count, loaded.Discrepancies.Count);
        foreach (var (expected, actual) in SampleResult.Discrepancies.Zip(loaded.Discrepancies))
        {
            Assert.Equal(expected.Type, actual.Type);
            Assert.Equal(expected.GuestDisplayName, actual.GuestDisplayName);
            Assert.Equal(expected.HostDisplayName, actual.HostDisplayName);
            Assert.Equal(expected.SourceDisplayName, actual.SourceDisplayName);
            Assert.Equal(expected.MatchedBy, actual.MatchedBy);
            Assert.Equal(expected.Detail, actual.Detail);
            Assert.Equal(expected.PossibleMatch, actual.PossibleMatch);
            Assert.Equal(expected.SourceValue, actual.SourceValue);
            Assert.Equal(expected.ProcessedValue, actual.ProcessedValue);
            Assert.Equal(expected.Days, actual.Days);
            Assert.Equal(expected.SourceRows, actual.SourceRows);
            Assert.Equal(expected.ProcessedRows, actual.ProcessedRows);
        }
    }

    [Fact]
    public async Task Failed_run_keeps_the_error_and_missing_headers()
    {
        var id = await CreateAsync();

        await Store().FailAsync(id, "Problem", "Missing columns", ["Ticket type", "Email"], CancellationToken.None);

        var run = await Store().GetAsync(id, CancellationToken.None);
        Assert.Equal(RunStatus.Failed, run!.Status);
        Assert.Equal("Problem", run.ErrorTitle);
        Assert.Equal(["Ticket type", "Email"], run.MissingHeaders);
        Assert.NotNull(run.CompletedAtUtc);
    }

    [Fact]
    public async Task Filters_by_status_type_guest_and_file()
    {
        var completed = await CreateCompletedAsync("RSVP export.xlsx");
        var queued = await CreateAsync("Other.xlsx");

        async Task<IEnumerable<Guid>> List(RunFilter filter) =>
            (await Store().ListAsync(filter, CancellationToken.None)).Runs.Select(r => r.Id);

        Assert.Equal([completed], await List(new RunFilter { Status = RunStatus.Completed }));
        Assert.Equal([queued], await List(new RunFilter { Status = RunStatus.Queued }));
        Assert.Equal([completed], await List(new RunFilter { Type = DiscrepancyType.EmailMismatch }));
        Assert.Equal([completed], await List(new RunFilter { Guest = "LEILANI" }));
        Assert.Equal([completed], await List(new RunFilter { Guest = "Bill Hartley" })); // Source spelling
        Assert.Equal([completed], await List(new RunFilter { Guest = "oscar reid" }));    // host of Maeve
        Assert.Empty(await List(new RunFilter { Guest = "Alice" }));                      // matched, no differences
        Assert.Equal([completed], await List(new RunFilter { File = "rsvp" }));
        Assert.Empty(await List(new RunFilter { File = "%" }));                            // wildcards are literal
    }

    [Fact]
    public async Task Lists_newest_first_in_pages()
    {
        for (var i = 0; i < RunStore.PageSize + 5; i++)
        {
            _clock.Now = _clock.Now.AddMinutes(1);
            await CreateAsync($"File {i}.xlsx");
        }

        var first = await Store().ListAsync(new RunFilter(), CancellationToken.None);
        var second = await Store().ListAsync(new RunFilter { Page = 2 }, CancellationToken.None);

        Assert.Equal(RunStore.PageSize + 5, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal($"File {RunStore.PageSize + 4}.xlsx", first.Runs[0].SourceFileName);
        Assert.Equal(5, second.Runs.Count);
        Assert.Equal("File 0.xlsx", second.Runs[^1].SourceFileName);
    }

    [Fact]
    public async Task Deleting_old_runs_also_deletes_their_results()
    {
        var old = await CreateCompletedAsync();
        _clock.Now = _clock.Now.AddDays(100);
        var recent = await CreateCompletedAsync();

        var deleted = await Store().DeleteOlderThanAsync(_clock.Now.UtcDateTime.AddDays(-90), CancellationToken.None);

        Assert.Equal(1, deleted);
        await using var db = _database.CreateContext();
        Assert.Equal([recent], await db.Runs.Select(r => r.Id).ToListAsync());
        Assert.False(await db.Discrepancies.AnyAsync(d => d.RunId == old));
        Assert.False(await db.Warnings.AnyAsync(w => w.RunId == old));
        Assert.True(await db.Discrepancies.AnyAsync(d => d.RunId == recent));
    }

    [Fact]
    public async Task Retention_service_deletes_runs_past_the_retention_period()
    {
        await CreateCompletedAsync();
        _clock.Now = _clock.Now.AddDays(31);
        var recent = await CreateAsync();

        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(_clock)
            .AddScoped(_ => _database.CreateContext())
            .AddScoped<RunStore>()
            .BuildServiceProvider();
        var retention = new RunRetentionService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ReconciliationOptions { RetentionDays = 30 }),
            _clock,
            NullLogger<RunRetentionService>.Instance);

        Assert.Equal(1, await retention.DeleteExpiredAsync(CancellationToken.None));
        await using var db = _database.CreateContext();
        Assert.Equal([recent], await db.Runs.Select(r => r.Id).ToListAsync());
    }

    [Fact]
    public async Task Runs_interrupted_by_a_restart_are_marked_failed()
    {
        var queued = await CreateAsync();
        var processing = await CreateAsync();
        await Store().MarkProcessingAsync(processing, CancellationToken.None);
        var completed = await CreateCompletedAsync();

        Assert.Equal(2, await Store().FailInterruptedAsync(CancellationToken.None));

        Assert.Equal(RunStatus.Failed, await Store().GetStatusAsync(queued, CancellationToken.None));
        Assert.Equal(RunStatus.Failed, await Store().GetStatusAsync(processing, CancellationToken.None));
        Assert.Equal(RunStatus.Completed, await Store().GetStatusAsync(completed, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_removes_a_run()
    {
        var id = await CreateCompletedAsync();

        Assert.True(await Store().DeleteAsync(id, CancellationToken.None));
        Assert.Null(await Store().GetAsync(id, CancellationToken.None));
        Assert.False(await Store().DeleteAsync(id, CancellationToken.None));
    }
}
