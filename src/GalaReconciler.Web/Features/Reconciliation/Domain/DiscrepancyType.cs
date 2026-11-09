namespace GalaReconciler.Web.Features.Reconciliation.Domain;

public enum DiscrepancyType
{
    GuestNotFound,
    DatesMismatch,
    MobileNumberMismatch,
    EmailMismatch,
}

public static class DiscrepancyTypeExtensions
{
    public static string ToDisplay(this DiscrepancyType type) => type switch
    {
        DiscrepancyType.GuestNotFound => "Guest not found",
        DiscrepancyType.DatesMismatch => "Dates mismatch",
        DiscrepancyType.MobileNumberMismatch => "Mobile mismatch",
        DiscrepancyType.EmailMismatch => "Email mismatch",
        _ => type.ToString(),
    };
}
