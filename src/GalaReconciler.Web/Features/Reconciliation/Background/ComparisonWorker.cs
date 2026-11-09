namespace GalaReconciler.Web.Features.Reconciliation.Background;

/// <summary>
/// Processes queued comparisons one at a time. Running them in sequence caps peak memory at one
/// comparison, however many people upload at once.
/// </summary>
public sealed class ComparisonWorker(
    ComparisonQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ComparisonWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ComparisonProcessor>().ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // E.g. the database was unavailable. Keep the worker alive for the next job; the run stays
                // queued/processing until the next restart marks it as interrupted.
                logger.LogError(ex, "Run {RunId} could not be processed", job.RunId);
            }
        }
    }
}
