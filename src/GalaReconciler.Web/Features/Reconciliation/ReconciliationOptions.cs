namespace GalaReconciler.Web.Features.Reconciliation;

/// <summary>
/// Web-only settings. The comparison itself is configured by <see cref="Core.ComparisonOptions"/>,
/// bound from the same configuration section.
/// </summary>
public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    /// <summary>Saved runs older than this many days are deleted automatically. 0 or less keeps them forever.</summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// How many uploads can wait for the background worker. Each holds both files in memory, so this caps
    /// memory used by waiting uploads at roughly this × 20 MB.
    /// </summary>
    public int MaxQueuedComparisons { get; set; } = 10;
}
