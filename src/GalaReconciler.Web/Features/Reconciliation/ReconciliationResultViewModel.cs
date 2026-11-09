using GalaReconciler.Core.Domain;
using GalaReconciler.Core;

namespace GalaReconciler.Web.Features.Reconciliation;

public sealed class ReconciliationResultViewModel(Guid runId, ReconciliationResult result)
{
    public static IReadOnlyList<DiscrepancyType> Types { get; } = Enum.GetValues<DiscrepancyType>();

    public Guid RunId { get; } = runId;

    public ReconciliationResult Result { get; } = result;

    public IReadOnlyList<Discrepancy> Discrepancies => Result.Discrepancies;

    public int CountOf(DiscrepancyType type) => Result.Discrepancies.Count(d => d.Type == type);

    public IEnumerable<IGrouping<string, ReconciliationWarning>> WarningsByFile => Result.Warnings.GroupBy(w => w.File);

    public string RunAtDisplay => Result.RunAt.ToString("d MMM yyyy, h:mm tt");

    public string DurationDisplay => Result.Duration.TotalSeconds < 1
        ? $"{Result.Duration.TotalMilliseconds:0} ms"
        : $"{Result.Duration.TotalSeconds:0.0} s";

    public static IReadOnlyList<string> FormatLocation(IReadOnlyList<RowReference> rows) => RowReference.Describe(rows);

    public static IReadOnlyList<string> Lines(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split('\n');
}
