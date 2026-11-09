using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;

namespace GalaReconciler.Core.Rules;

/// <summary>
/// Compares one matched Source/Processed pair. Rules run in the order they're registered in
/// <see cref="ComparisonServiceCollectionExtensions.AddComparison"/>.
/// </summary>
public interface IDiscrepancyRule
{
    DiscrepancyType Type { get; }

    /// <returns>At most one discrepancy per person, or null when the pair agrees.</returns>
    Discrepancy? Evaluate(MatchedPair pair);
}

public static class DiscrepancyFactory
{
    public static Discrepancy ForPair(MatchedPair pair, DiscrepancyType type, string detail) => new()
    {
        Type = type,
        GuestDisplayName = pair.DisplayName,
        HostDisplayName = pair.HostDisplayName,
        SourceDisplayName = pair.SourceDisplayName,
        MatchedBy = pair.MatchedBy,
        SourceRows = pair.Source.Rows,
        ProcessedRows = pair.Processed.Rows,
        Detail = detail,
    };
}
