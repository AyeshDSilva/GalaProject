namespace GalaReconciler.Web.Features.Reconciliation.Domain;

/// <summary>Something odd found while reading a file that didn't stop the comparison.</summary>
public sealed record ReconciliationWarning(string File, string Message);
