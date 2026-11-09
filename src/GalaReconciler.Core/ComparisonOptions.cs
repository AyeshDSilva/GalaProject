namespace GalaReconciler.Core;

/// <summary>Settings that change how the two files are compared.</summary>
public sealed class ComparisonOptions
{
    /// <summary>
    /// When false (default), "not attending" and "no response" are treated as the same thing.
    /// When true, a day is also flagged if one side says N and the other is blank.
    /// </summary>
    public bool StrictNotAttending { get; set; }
}
