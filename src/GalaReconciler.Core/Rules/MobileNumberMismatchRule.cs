using GalaReconciler.Core.Domain;

namespace GalaReconciler.Core.Rules;

public sealed class MobileNumberMismatchRule : SetMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.MobileNumberMismatch;

    protected override IReadOnlySet<string> SelectValues(GuestRecord record) => record.Phones;
}
