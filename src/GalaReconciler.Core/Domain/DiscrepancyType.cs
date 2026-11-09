namespace GalaReconciler.Core.Domain;

public enum DiscrepancyType
{
    GuestNotFound,
    DatesMismatch,
    MobileNumberMismatch,
    EmailMismatch,
    TitleMismatch,
    PostNominalMismatch,
    PositionMismatch,
}

public static class DiscrepancyTypeExtensions
{
    public static string ToDisplay(this DiscrepancyType type) => type switch
    {
        DiscrepancyType.GuestNotFound => "Guest not found",
        DiscrepancyType.DatesMismatch => "Dates mismatch",
        DiscrepancyType.MobileNumberMismatch => "Mobile mismatch",
        DiscrepancyType.EmailMismatch => "Email mismatch",
        DiscrepancyType.TitleMismatch => "Title mismatch",
        DiscrepancyType.PostNominalMismatch => "Post nominal mismatch",
        DiscrepancyType.PositionMismatch => "Position mismatch",
        _ => type.ToString(),
    };
}
