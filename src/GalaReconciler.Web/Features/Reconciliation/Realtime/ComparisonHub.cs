using GalaReconciler.Web.Features.Reconciliation.Persistence;
using Microsoft.AspNetCore.SignalR;

namespace GalaReconciler.Web.Features.Reconciliation.Realtime;

/// <summary>
/// Pushes run status changes to the run page. Clients call <see cref="WatchRun"/> and then receive
/// <c>RunStatusChanged(runId, status)</c> messages.
/// </summary>
public sealed class ComparisonHub(RunStore runs) : Hub
{
    public const string Path = "/hubs/comparisons";
    public const string RunStatusChanged = nameof(RunStatusChanged);

    /// <summary>
    /// Subscribes to a run and returns its current status, so a run that finished before the browser
    /// connected isn't missed.
    /// </summary>
    public async Task<string?> WatchRun(Guid runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(runId), Context.ConnectionAborted);
        var status = await runs.GetStatusAsync(runId, Context.ConnectionAborted);
        return status?.ToString();
    }

    public static string GroupName(Guid runId) => $"run:{runId}";
}

public interface IRunNotifier
{
    Task RunStatusChangedAsync(Guid runId, RunStatus status, CancellationToken cancellationToken);
}

public sealed class SignalRRunNotifier(IHubContext<ComparisonHub> hub, ILogger<SignalRRunNotifier> logger) : IRunNotifier
{
    public async Task RunStatusChangedAsync(Guid runId, RunStatus status, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Group(ComparisonHub.GroupName(runId))
                .SendAsync(ComparisonHub.RunStatusChanged, runId, status.ToString(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A missed notification only delays the page; the run itself is already saved.
            logger.LogWarning(ex, "Couldn't notify watchers of run {RunId}", runId);
        }
    }
}
