using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;

namespace GalaReconciler.Core.Rules;

/// <summary>
/// Shared logic for comparing a set of normalized values (phones, emails). One value in common is enough:
/// the master list often holds two numbers in one Phone cell, or the address in either Email 1 or Email 2,
/// and only one of them needs to match. Raised when there's nothing in common, or when one side is blank and
/// the other isn't.
/// </summary>
public abstract class SetMismatchRule : IDiscrepancyRule
{
    public abstract DiscrepancyType Type { get; }

    protected abstract IReadOnlySet<string> SelectValues(GuestRecord record);

    public Discrepancy? Evaluate(MatchedPair pair)
    {
        // An extra ticket carries the host's contact details, so there's nothing of the guest's to compare.
        if (pair.Source.IsUnnamedGuest)
        {
            return null;
        }

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
        else if (source.Overlaps(processed))
        {
            return null;
        }
        else
        {
            detail = "Nothing in common";
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
