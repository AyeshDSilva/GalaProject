using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace GalaReconciler.Web.Features.Reconciliation.Background;

/// <summary>The uploaded files for one run, held in memory only until the worker has processed them.</summary>
public sealed record ComparisonJob(
    Guid RunId,
    byte[] SourceFile,
    string SourceFileName,
    byte[] ProcessedFile,
    string ProcessedFileName);

/// <summary>
/// In-memory queue of comparisons waiting for <see cref="ComparisonWorker"/>. Bounded so a burst of uploads
/// can't hold an unlimited number of files in memory.
/// </summary>
public sealed class ComparisonQueue(IOptions<ReconciliationOptions> options)
{
    private readonly Channel<ComparisonJob> _channel = Channel.CreateBounded<ComparisonJob>(
        new BoundedChannelOptions(Math.Max(1, options.Value.MaxQueuedComparisons))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ChannelReader<ComparisonJob> Reader => _channel.Reader;

    /// <returns>False when the queue is full.</returns>
    public bool TryEnqueue(ComparisonJob job) => _channel.Writer.TryWrite(job);
}
