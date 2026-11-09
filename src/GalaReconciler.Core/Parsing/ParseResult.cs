using GalaReconciler.Core.Domain;

namespace GalaReconciler.Core.Parsing;

/// <summary>One record per person (match keys are unique), plus anything worth flagging to the user.</summary>
public sealed record ParseResult(IReadOnlyList<GuestRecord> Records, IReadOnlyList<ReconciliationWarning> Warnings);
