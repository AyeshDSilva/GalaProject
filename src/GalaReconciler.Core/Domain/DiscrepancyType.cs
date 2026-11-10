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

    /// <summary>Console app only for now (see <see cref="DiscrepancyTypeExtensions.IsConsoleOnly"/>).</summary>
    DietaryMismatch,

    /// <summary>Console app only for now (see <see cref="DiscrepancyTypeExtensions.IsConsoleOnly"/>).</summary>
    AccessibilityMismatch,
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
        DiscrepancyType.DietaryMismatch => "Dietary mismatch",
        DiscrepancyType.AccessibilityMismatch => "Accessibility mismatch",
        _ => type.ToString(),
    };

    /// <summary>
    /// Checked by the console app only (<see cref="ComparisonServiceCollectionExtensions.AddRequirementChecks"/>).
    /// The web app never produces these, and leaves them out of its counts and cards.
    /// </summary>
    public static bool IsConsoleOnly(this DiscrepancyType type) =>
        type is DiscrepancyType.DietaryMismatch or DiscrepancyType.AccessibilityMismatch;
}
