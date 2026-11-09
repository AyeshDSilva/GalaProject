using GalaReconciler.Web.Features.Reconciliation.Domain;

namespace GalaReconciler.Web.Features.Reconciliation.Rules;

public sealed class EmailMismatchRule : SetMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.EmailMismatch;

    protected override IReadOnlySet<string> SelectValues(GuestRecord record) => record.Emails;
}
