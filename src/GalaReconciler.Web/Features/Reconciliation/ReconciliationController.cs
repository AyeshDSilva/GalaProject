using GalaReconciler.Web.Features.Reconciliation.Background;
using GalaReconciler.Web.Features.Reconciliation.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace GalaReconciler.Web.Features.Reconciliation;

public sealed class ReconciliationController(RunStore runs, ComparisonQueue queue) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new UploadFilesViewModel());

    /// <summary>Bookmarking or refreshing the old results URL goes back to the upload form.</summary>
    [HttpGet]
    [ActionName(nameof(Compare))]
    public IActionResult CompareGet() => RedirectToAction(nameof(Index));

    /// <summary>Saves a queued run, hands the files to the background worker and shows the run page.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadFilesViewModel.MaxRequestBytes)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = UploadFilesViewModel.MaxRequestBytes,
        // Keep every uploaded file in memory. Above this threshold ASP.NET Core would buffer it to a temp file.
        MemoryBufferThreshold = (int)UploadFilesViewModel.MaxRequestBytes)]
    public async Task<IActionResult> Compare(UploadFilesViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        var sourceFileName = Path.GetFileName(model.SourceFile!.FileName);
        var processedFileName = Path.GetFileName(model.ProcessedFile!.FileName);
        var sourceFile = await ReadAllBytesAsync(model.SourceFile, cancellationToken);
        var processedFile = await ReadAllBytesAsync(model.ProcessedFile, cancellationToken);

        var runId = await runs.CreateQueuedAsync(sourceFileName, sourceFile.Length, processedFileName, processedFile.Length, cancellationToken);
        if (!queue.TryEnqueue(new ComparisonJob(runId, sourceFile, sourceFileName, processedFile, processedFileName)))
        {
            await runs.DeleteAsync(runId, cancellationToken);
            model.Error = new UploadError(
                "The server is busy",
                "Too many comparisons are already waiting. Try again in a minute.",
                []);
            return View(nameof(Index), model);
        }

        return RedirectToAction(nameof(ShowRun), new { id = runId });
    }

    [HttpGet("runs")]
    public async Task<IActionResult> ListRuns([FromQuery] RunFilter filter, CancellationToken cancellationToken) =>
        View("Runs", await runs.ListAsync(filter, cancellationToken));

    /// <summary>The results once the run has completed; otherwise its progress or error.</summary>
    [HttpGet("runs/{id:guid}")]
    public async Task<IActionResult> ShowRun(Guid id, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(id, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        return run.Status == RunStatus.Completed
            ? View("Results", new ReconciliationResultViewModel(run.Id, RunStore.ToResult(run)))
            : View("RunStatus", run);
    }

    [HttpPost("runs/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRun(Guid id, CancellationToken cancellationToken)
    {
        await runs.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(ListRuns));
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var bytes = new byte[file.Length];
        await using var stream = file.OpenReadStream();
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }
}
