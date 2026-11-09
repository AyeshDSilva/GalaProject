using GalaReconciler.Web.Features.Reconciliation.Domain;

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

    /// <summary>"Thurs_Sun Suite RSVP · rows 2, 4, 6, 8 · Order Q7RT2MXA", one line per sheet.</summary>
    public static IReadOnlyList<string> FormatLocation(IReadOnlyList<RowReference> rows) =>
        rows.GroupBy(r => r.SheetName)
            .Select(sheet =>
            {
                var numbers = sheet.Select(r => r.RowNumber).Distinct().Order().ToList();
                var orders = sheet.Select(r => r.OrderId).OfType<string>().Distinct().ToList();
                var text = $"{sheet.Key} · {(numbers.Count == 1 ? "row" : "rows")} {string.Join(", ", numbers)}";
                return orders.Count == 0 ? text : $"{text} · {(orders.Count == 1 ? "Order" : "Orders")} {string.Join(", ", orders)}";
            })
            .ToList();

    public static IReadOnlyList<string> Lines(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split('\n');
}
