using GalaReconciler.Web.Features.Reconciliation.Persistence;
using Microsoft.Extensions.Options;

namespace GalaReconciler.Web.Features.Reconciliation.Background;

/// <summary>
/// Deletes runs older than <see cref="ReconciliationOptions.RetentionDays"/> at startup and then every few hours,
/// since saved results contain guests' names, phone numbers and emails.
/// </summary>
public sealed class RunRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReconciliationOptions> options,
    TimeProvider timeProvider,
    ILogger<RunRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.RetentionDays <= 0)
        {
            return; // Keep runs forever.
        }

        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            try
            {
                await DeleteExpiredAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Deleting expired runs failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays);
        await using var scope = scopeFactory.CreateAsyncScope();
        var deleted = await scope.ServiceProvider.GetRequiredService<RunStore>().DeleteOlderThanAsync(cutoff, cancellationToken);
        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} runs older than {Days} days", deleted, options.Value.RetentionDays);
        }

        return deleted;
    }
}
