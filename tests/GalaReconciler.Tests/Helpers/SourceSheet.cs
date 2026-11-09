namespace GalaReconciler.Tests.Helpers;

/// <summary>A ticket row in the Source RSVP export. Buyer defaults to the attendee.</summary>
public sealed record SourceTicket(string TicketType, string FirstName, string LastName, string OrderId = "ORDER1")
{
    public string? Title { get; init; }

    public object? Mobile { get; init; }

    public object? Telephone { get; init; }

    public string? Email { get; init; }

    public string? BuyerFirstName { get; init; }

    public string? BuyerLastName { get; init; }

    public string? BuyerMobile { get; init; }

    public string? BuyerEmail { get; init; }

    /// <summary>Leave the attendee-section First Name/Last Name blank (names only in columns B/C).</summary>
    public bool AttendeeNameBlank { get; init; }

    /// <summary>Put a different name in columns B/C than in the attendee section.</summary>
    public (string First, string Last)? TicketHolderName { get; init; }
}

public static class SourceSheet
{
    public static IReadOnlyList<string> Headers { get; } =
    [
        "Event", "First name", "Last name", "Ticket type", "Order id", "Ticket no.", "Order date & time", "Timezone",
        "Buyer first name", "Buyer last name", "Buyer mobile", "Buyer email", "Postal Address", "Postal Address street",
        "Postal Address suburb", "Postal Address postcode", "Postal Address state", "Postal Address country", "Title",
        "Please specify your title", "First Name", "Last Name", "Post nominal", "Please specify your post nominal",
        "Mobile", "Telephone", "Email", "Position title", "Organisation", "Dietary requirements",
        "Please specify any dietary requirements",
    ];

    private static int _ticketNumber = 1000;

    public static WorkbookBuilder AddSourceSheet(this WorkbookBuilder builder, string sheetName, params SourceTicket[] tickets) =>
        builder.Sheet(sheetName, Headers, tickets.Select(ToRow));

    public static IReadOnlyList<object?> ToRow(SourceTicket ticket)
    {
        var row = new object?[Headers.Count];

        // Exact, case-sensitive: "First name" (column B) and "First Name" (attendee section) are different.
        void Set(string header, object? value) => row[IndexOf(header)] = value;

        var holder = ticket.TicketHolderName ?? (ticket.FirstName, ticket.LastName);
        Set("Event", "GALA 2026");
        Set("First name", holder.First);
        Set("Last name", holder.Last);
        Set("Ticket type", ticket.TicketType);
        Set("Order id", ticket.OrderId);
        Set("Ticket no.", $"T{Interlocked.Increment(ref _ticketNumber)}");
        Set("Order date & time", "2026-09-01 10:15");
        Set("Timezone", "Australia/Adelaide");
        Set("Buyer first name", ticket.BuyerFirstName ?? ticket.FirstName);
        Set("Buyer last name", ticket.BuyerLastName ?? ticket.LastName);
        Set("Buyer mobile", ticket.BuyerMobile);
        Set("Buyer email", ticket.BuyerEmail);
        Set("Title", ticket.Title);
        if (!ticket.AttendeeNameBlank)
        {
            Set("First Name", ticket.FirstName);
            Set("Last Name", ticket.LastName);
        }

        Set("Mobile", ticket.Mobile);
        Set("Telephone", ticket.Telephone);
        Set("Email", ticket.Email);
        return row;
    }

    private static int IndexOf(string header)
    {
        for (var i = 0; i < Headers.Count; i++)
        {
            if (string.Equals(Headers[i], header, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new ArgumentException($"Unknown Source header '{header}'.");
    }
}
