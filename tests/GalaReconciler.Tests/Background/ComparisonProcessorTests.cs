using GalaReconciler.Tests.Helpers;
using GalaReconciler.Web.Features.Reconciliation.Background;
using GalaReconciler.Web.Features.Reconciliation.Persistence;
using GalaReconciler.Web.Features.Reconciliation.Realtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace GalaReconciler.Tests.Background;

public sealed class ComparisonProcessorTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly RecordingNotifier _notifier = new();

    public void Dispose() => _database.Dispose();

    private RunStore Store() => new(_database.CreateContext(), TimeProvider.System);

    private async Task<Run> ProcessAsync(WorkbookBuilder source, WorkbookBuilder processed)
    {
        var id = await Store().CreateQueuedAsync("Source.xlsx", 1, "Processed.xlsx", 1, CancellationToken.None);
        var job = new ComparisonJob(id, source.ToStream().ToArray(), "Source.xlsx", processed.ToStream().ToArray(), "Processed.xlsx");
        var processor = new ComparisonProcessor(TestHandlers.CreateCompareFilesHandler(), Store(), _notifier, NullLogger<ComparisonProcessor>.Instance);

        await processor.ProcessAsync(job, CancellationToken.None);

        return (await Store().GetAsync(id, CancellationToken.None))!;
    }

    [Fact]
    public async Task Saves_results_and_notifies_processing_then_completed()
    {
        var run = await ProcessAsync(SampleWorkbookBuilder.Source(), SampleWorkbookBuilder.Processed());

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(10, run.Discrepancies.Count);
        Assert.Equal([(run.Id, RunStatus.Processing), (run.Id, RunStatus.Completed)], _notifier.Calls);
    }

    [Fact]
    public async Task A_file_problem_fails_the_run_with_a_friendly_error()
    {
        // The files the wrong way round: the master list has none of the RSVP export's columns.
        var run = await ProcessAsync(SampleWorkbookBuilder.Processed(), SampleWorkbookBuilder.Source());

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Contains("Source file (RSVP export)", run.ErrorTitle);
        Assert.Contains("Source.xlsx", run.ErrorTitle);
        Assert.Contains("Ticket type", run.MissingHeaders);
        Assert.Empty(run.Discrepancies);
        Assert.Equal(RunStatus.Failed, _notifier.Calls[^1].Status);
    }

    [Fact]
    public async Task A_file_that_is_not_a_workbook_fails_the_run()
    {
        var id =await Store().CreateQueuedAsync("Source.xlsx", 1, "Processed.xlsx", 1, CancellationToken.None);
        var job = new ComparisonJob(id, "not a workbook"u8.ToArray(), "Source.xlsx", SampleWorkbookBuilder.Processed().ToStream().ToArray(), "Processed.xlsx");
        var processor = new ComparisonProcessor(TestHandlers.CreateCompareFilesHandler(), Store(), _notifier, NullLogger<ComparisonProcessor>.Instance);

        await processor.ProcessAsync(job, CancellationToken.None);

        var run = await Store().GetAsync(id, CancellationToken.None);
        Assert.Equal(RunStatus.Failed, run!.Status);
        Assert.Contains("couldn't be opened", run.ErrorMessage);
    }

    private sealed class RecordingNotifier : IRunNotifier
    {
        public List<(Guid RunId, RunStatus Status)> Calls { get; } = [];

        public Task RunStatusChangedAsync(Guid runId, RunStatus status, CancellationToken cancellationToken)
        {
            Calls.Add((runId, status));
            return Task.CompletedTask;
        }
    }
}
