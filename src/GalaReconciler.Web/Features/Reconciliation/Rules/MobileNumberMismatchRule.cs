using GalaReconciler.Web.Features.Reconciliation.Domain;

namespace GalaReconciler.Web.Features.Reconciliation.Rules;

public sealed class MobileNumberMismatchRule : SetMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.MobileNumberMismatch;

    protected override IReadOnlySet<string> SelectValues(GuestRecord record) => record.Phones;
}
