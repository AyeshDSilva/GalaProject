using System.Text.RegularExpressions;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Normalization;

namespace GalaReconciler.Core.Parsing;

/// <summary>Parses Source "Ticket type" values such as "Friday not attending" or "Grid Gala attending".</summary>
public static class TicketTypeParser
{
    // Built from EventDays so a new day only needs adding there.
    private static readonly Regex Pattern = new(
        "^(" + string.Join('|', EventDays.All.Select(d => Regex.Escape(d.TicketTypeLabel).Replace(@"\ ", @"\s+"))) + @")\s+(not\s+)?attending$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryParse(string? ticketType, out EventDay day, out AttendanceState state)
    {
        day = default;
        state = AttendanceState.NoResponse;

        var match = Pattern.Match(NameNormalizer.CleanDisplay(ticketType));
        if (!match.Success)
        {
            return false;
        }

        var label = NameNormalizer.CleanDisplay(match.Groups[1].Value);
        day = EventDays.All.First(d => string.Equals(d.TicketTypeLabel, label, StringComparison.OrdinalIgnoreCase)).Day;
        state = match.Groups[2].Success ? AttendanceState.NotAttending : AttendanceState.Attending;
        return true;
    }

    // The Gala RSVP export has no day in its ticket types: "RSVP - Attending" / "RSVP - Not attending".
    private static readonly Regex GalaPattern = new(
        @"^RSVP\s*[-–—:]?\s*(not\s+)?attending$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Ticket types in the separate Gala RSVP export: "RSVP - Attending" or "RSVP - Not attending", always Grid Gala.
    /// A day ticket such as "Grid Gala attending" is accepted too.
    /// </summary>
    public static bool TryParseGala(string? ticketType, out EventDay day, out AttendanceState state)
    {
        if (TryParse(ticketType, out day, out state))
        {
            return true;
        }

        var match = GalaPattern.Match(NameNormalizer.CleanDisplay(ticketType));
        if (!match.Success)
        {
            return false;
        }

        day = EventDay.GridGala;
        state = match.Groups[1].Success ? AttendanceState.NotAttending : AttendanceState.Attending;
        return true;
    }

    /// <summary>True for the Gala export's own "RSVP - …" ticket types (not day tickets).</summary>
    public static bool IsGalaRsvp(string? ticketType) => GalaPattern.IsMatch(NameNormalizer.CleanDisplay(ticketType));
}
