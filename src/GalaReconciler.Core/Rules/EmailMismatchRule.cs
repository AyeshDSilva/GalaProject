using GalaReconciler.Core.Domain;

namespace GalaReconciler.Core.Rules;

public sealed class EmailMismatchRule : SetMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.EmailMismatch;

    protected override IReadOnlySet<string> SelectValues(GuestRecord record) => record.Emails;
}
