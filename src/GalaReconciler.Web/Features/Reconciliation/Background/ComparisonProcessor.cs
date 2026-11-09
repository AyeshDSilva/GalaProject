using GalaReconciler.Web.Features.Reconciliation.Parsing;
using GalaReconciler.Web.Features.Reconciliation.Persistence;
using GalaReconciler.Web.Features.Reconciliation.Realtime;

namespace GalaReconciler.Web.Features.Reconciliation.Background;

/// <summary>Runs one queued comparison, saves the outcome and tells any watching browser.</summary>
public sealed class ComparisonProcessor(
    CompareFilesHandler handler,
    RunStore runs,
    IRunNotifier notifier,
    ILogger<ComparisonProcessor> logger)
{
    public async Task ProcessAsync(ComparisonJob job, CancellationToken cancellationToken)
    {
        await runs.MarkProcessingAsync(job.RunId, cancellationToken);
        await notifier.RunStatusChangedAsync(job.RunId, RunStatus.Processing, cancellationToken);

        RunStatus outcome;
        try
        {
            using var source = new MemoryStream(job.SourceFile, writable: false);
            using var processed = new MemoryStream(job.ProcessedFile, writable: false);
            var result = handler.Handle(new CompareFilesRequest(source, job.SourceFileName, processed, job.ProcessedFileName));

            await runs.CompleteAsync(job.RunId, result, cancellationToken);
            outcome = RunStatus.Completed;
        }
        catch (WorkbookFormatException ex)
        {
            var fileName = ex.FileLabel == SourceRsvpParser.FileLabel ? job.SourceFileName : job.ProcessedFileName;
            await runs.FailAsync(
                job.RunId,
                $"There's a problem with the {ex.FileLabel} “{fileName}”",
                ex.Message,
                ex.MissingHeaders,
                cancellationToken);
            outcome = RunStatus.Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Run {RunId} comparing {SourceFile} with {ProcessedFile} failed", job.RunId, job.SourceFileName, job.ProcessedFileName);
            await runs.FailAsync(
                job.RunId,
                "Something went wrong while comparing the files",
                "Check that both files are valid .xlsx workbooks in the expected format and try again.",
                [],
                cancellationToken);
            outcome = RunStatus.Failed;
        }

        await notifier.RunStatusChangedAsync(job.RunId, outcome, cancellationToken);
    }
}
