using System.Text.RegularExpressions;
using GalaReconciler.Web.Features.Reconciliation.Domain;
using GalaReconciler.Web.Features.Reconciliation.Normalization;

namespace GalaReconciler.Web.Features.Reconciliation.Parsing;

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
}
