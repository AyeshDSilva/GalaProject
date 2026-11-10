using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;
using GalaReconciler.Core.Normalization;

namespace GalaReconciler.Core.Rules;

/// <summary>
/// Dietary and accessibility requirements: every requirement the guest gave in the RSVP exports must appear in the
/// master list's cell. The exports' drop-down and "Please specify" answers are combined and split into parts (see
/// <see cref="RequirementNormalizer"/>), and each part is looked for in the master list's text, ignoring case,
/// spacing and punctuation. Extra detail in the master list is fine; nothing in the exports raises nothing.
/// </summary>
public abstract class RequirementsMismatchRule : IDiscrepancyRule
{
    public abstract DiscrepancyType Type { get; }

    protected abstract List<string> SelectValue(GuestRecord record);

    public Discrepancy? Evaluate(MatchedPair pair)
    {
        // An unnamed "guest" in the master list has no row of its own to hold requirements.
        if (pair.Processed.IsUnnamedGuest)
        {
            return null;
        }

        var source = SelectValue(pair.Source);
        if (source.Count == 0)
        {
            return null;
        }

        var processed = SelectValue(pair.Processed).Count == 0 ? null : string.Join('\n', SelectValue(pair.Processed));
        var missing = source.Where(part => !RequirementNormalizer.IsCoveredBy(part, processed)).ToList();
        if (missing.Count == 0)
        {
            return null;
        }

        var detail = processed is null ? "Missing in Processed" : $"Not in Processed: {string.Join("; ", missing)}";
        return DiscrepancyFactory.ForPair(pair, Type, detail) with
        {
            SourceValue = string.Join('\n', source),
            ProcessedValue = processed,
        };
    }
}

/// <summary>Dietary requirements ("Dietary requirements" + "Please specify any dietary requirements"), compared with Dietary.</summary>
public sealed class DietaryMismatchRule : RequirementsMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.DietaryMismatch;

    protected override List<string> SelectValue(GuestRecord record) => record.Dietary;
}

/// <summary>Accessibility requirements, compared with Accessibility.</summary>
public sealed class AccessibilityMismatchRule : RequirementsMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.AccessibilityMismatch;

    protected override List<string> SelectValue(GuestRecord record) => record.Accessibility;
}
