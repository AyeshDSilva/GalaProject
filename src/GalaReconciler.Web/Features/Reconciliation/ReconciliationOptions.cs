namespace GalaReconciler.Web.Features.Reconciliation;

public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";

    /// <summary>
    /// When false (default), "not attending" and "no response" are treated as the same thing.
    /// When true, a day is also flagged if one side says N and the other is blank.
    /// </summary>
    public bool StrictNotAttending { get; set; }

    /// <summary>Saved runs older than this many days are deleted automatically. 0 or less keeps them forever.</summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// How many uploads can wait for the background worker. Each holds both files in memory, so this caps
    /// memory used by waiting uploads at roughly this × 20 MB.
    /// </summary>
    public int MaxQueuedComparisons { get; set; } = 10;
}
