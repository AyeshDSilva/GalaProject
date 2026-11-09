using GalaReconciler.Web.Features.Reconciliation.Domain;
using GalaReconciler.Web.Features.Reconciliation.Matching;

namespace GalaReconciler.Web.Features.Reconciliation.Rules;

/// <summary>
/// Shared logic for comparing a set of normalized values (phones, emails): raise when either side has a
/// value the other doesn't, or when one side is blank and the other isn't.
/// </summary>
public abstract class SetMismatchRule : IDiscrepancyRule
{
    public abstract DiscrepancyType Type { get; }

    protected abstract IReadOnlySet<string> SelectValues(GuestRecord record);

    public Discrepancy? Evaluate(MatchedPair pair)
    {
        var source = SelectValues(pair.Source);
        var processed = SelectValues(pair.Processed);

        string detail;
        if (source.Count == 0 && processed.Count == 0)
        {
            return null;
        }
        else if (source.Count == 0)
        {
            detail = "Missing in Source";
        }
        else if (processed.Count == 0)
        {
            detail = "Missing in Processed";
        }
        else
        {
            var onlyInSource = source.Except(processed).Order(StringComparer.Ordinal).ToList();
            var onlyInProcessed = processed.Except(source).Order(StringComparer.Ordinal).ToList();
            if (onlyInSource.Count == 0 && onlyInProcessed.Count == 0)
            {
                return null;
            }

            var parts = new List<string>();
            if (onlyInSource.Count > 0)
            {
                parts.Add($"Only in Source: {string.Join(", ", onlyInSource)}");
            }

            if (onlyInProcessed.Count > 0)
            {
                parts.Add($"Only in Processed: {string.Join(", ", onlyInProcessed)}");
            }

            detail = string.Join("; ", parts);
        }

        return DiscrepancyFactory.ForPair(pair, Type, detail) with
        {
            SourceValue = Join(source),
            ProcessedValue = Join(processed),
        };
    }

    private static string? Join(IReadOnlySet<string> values) =>
        values.Count == 0 ? null : string.Join('\n', values.Order(StringComparer.Ordinal));
}
