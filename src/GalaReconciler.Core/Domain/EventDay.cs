namespace GalaReconciler.Core.Domain;

public enum EventDay
{
    Thursday,
    Friday,
    Saturday,
    Sunday,
    GridGala,
}

/// <summary>How an <see cref="EventDay"/> is written in each file and shown in the UI.</summary>
/// <param name="TicketTypeLabel">Day prefix in the Source "Ticket type" column, e.g. "Grid Gala attending".</param>
/// <param name="ProcessedHeader">Column header in the Processed master guest list.</param>
/// <param name="ShortLabel">Compact label for the results day grid.</param>
public sealed record EventDayInfo(EventDay Day, string TicketTypeLabel, string ProcessedHeader, string ShortLabel);

/// <summary>
/// The single place that maps event days to file text. To add a day, add an <see cref="EventDay"/>
/// value and one entry here; parsing, rules and the UI all pick it up.
/// </summary>
public static class EventDays
{
    public static IReadOnlyList<EventDayInfo> All { get; } =
    [
        new(EventDay.Thursday, "Thursday", "THURS", "Thu"),
        new(EventDay.Friday, "Friday", "FRI", "Fri"),
        new(EventDay.Saturday, "Saturday", "SAT", "Sat"),
        new(EventDay.Sunday, "Sunday", "SUN", "Sun"),
        new(EventDay.GridGala, "Grid Gala", "Grid Gala", "Gala"),
    ];

    public static EventDayInfo Get(EventDay day) => All.First(info => info.Day == day);
}
