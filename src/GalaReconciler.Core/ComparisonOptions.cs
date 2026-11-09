namespace GalaReconciler.Core;

/// <summary>Settings that change how the two files are compared.</summary>
public sealed class ComparisonOptions
{
    /// <summary>
    /// When false (default), a day with no ticket in the RSVP export isn't flagged against N in the master
    /// list (the guest may have declined outside the ticketing system). When true, it is.
    /// </summary>
    public bool StrictNotAttending { get; set; }
}
