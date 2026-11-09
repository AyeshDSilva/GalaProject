using GalaReconciler.Web.Features.Reconciliation.Domain;

namespace GalaReconciler.Web.Features.Reconciliation.Parsing;

/// <summary>One record per person (match keys are unique), plus anything worth flagging to the user.</summary>
public sealed record ParseResult(IReadOnlyList<GuestRecord> Records, IReadOnlyList<ReconciliationWarning> Warnings);
