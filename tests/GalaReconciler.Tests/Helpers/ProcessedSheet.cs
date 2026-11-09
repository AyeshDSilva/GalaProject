namespace GalaReconciler.Tests.Helpers;

/// <summary>A row in the Processed master guest list. Day values: "Y", "N", a number, or null.</summary>
public sealed record ProcessedGuest(string FirstName, string Surname)
{
    public string? Prenom { get; init; }

    public object? Thurs { get; init; }

    public object? Fri { get; init; }

    public object? Sat { get; init; }

    public object? Sun { get; init; }

    public object? GridGala { get; init; }

    public string? Postnom { get; init; }

    public string? GuestName { get; init; }

    public string? Position { get; init; }

    public string? Organisation { get; init; }

    public object? Phone { get; init; }

    public string? Email1 { get; init; }

    public string? Email2 { get; init; }

    public string? Dietary { get; init; }

    public string? Accessibility { get; init; }

    public string? Comments { get; init; }
}

public static class ProcessedSheet
{
    /// <summary>
    /// Mirrors the real sheet: column A has a blank header, and "Table" / "Invite list" are hidden columns
    /// sitting between the columns we care about.
    /// </summary>
    public static IReadOnlyList<string> Headers { get; } =
    [
        "", "Table", "THURS", "FRI", "SAT", "SUN", "Grid Gala", "Prenom", "First Name", "Surname", "Postnom",
        "Guest Name", "Position", "Organisation", "Phone", "Email 1", "Email 2", "Dietary", "Accessibility",
        "Comments", "Invite list",
    ];

    public static WorkbookBuilder AddProcessedSheet(this WorkbookBuilder builder, string sheetName, params ProcessedGuest?[] guests) =>
        builder.Sheet(sheetName, Headers, guests.Select(ToRow), sheet =>
        {
            sheet.Column(2).Hide();
            sheet.Column(Headers.Count).Hide();
        });

    /// <summary>A null guest produces a fully blank row.</summary>
    public static IReadOnlyList<object?> ToRow(ProcessedGuest? guest)
    {
        if (guest is null)
        {
            return new object?[Headers.Count];
        }

        object?[] row =
        [
            "•", "T1", guest.Thurs, guest.Fri, guest.Sat, guest.Sun, guest.GridGala, guest.Prenom, guest.FirstName,
            guest.Surname, guest.Postnom, guest.GuestName, guest.Position, guest.Organisation, guest.Phone, guest.Email1,
            guest.Email2, guest.Dietary, guest.Accessibility, guest.Comments, "VIP",
        ];
        return row;
    }
}
