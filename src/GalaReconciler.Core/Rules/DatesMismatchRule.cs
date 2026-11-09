using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;
using Microsoft.Extensions.Options;

namespace GalaReconciler.Core.Rules;

/// <summary>
/// Flags days where the master list doesn't record what the RSVP export says. A day with no ticket is fine
/// against "not invited" or "invited, no response"; see <see cref="Differs"/> and <see cref="ComparisonOptions.StrictNotAttending"/>.
/// </summary>
public sealed class DatesMismatchRule(IOptions<ComparisonOptions> options) : IDiscrepancyRule
{
    public DiscrepancyType Type => DiscrepancyType.DatesMismatch;

    public Discrepancy? Evaluate(MatchedPair pair)
    {
        var strict = options.Value.StrictNotAttending;
        var days = EventDays.All
            .Select(info =>
            {
                var source = pair.Source.GetAttendance(info.Day);
                var processed = pair.Processed.GetAttendance(info.Day);
                return new DayComparison(info.Day, source, processed, Differs(source, processed, strict));
            })
            .ToList();

        var differing = days.Where(d => d.IsDifferent).ToList();
        if (differing.Count == 0)
        {
            return null;
        }

        var labels = string.Join(", ", differing.Select(d => EventDays.Get(d.Day).ShortLabel));
        return DiscrepancyFactory.ForPair(pair, Type, $"Differs on {labels}") with
        {
            Days = days,
            SourceValue = Describe(differing, d => d.Source),
            ProcessedValue = Describe(differing, d => d.Processed),
        };
    }

    /// <summary>
    /// Export "attending" must be Y and "not attending" must be N. No ticket for the day matches a blank
    /// (not invited) or 1 (invited, no response yet); it differs from Y, and from N only in strict mode
    /// (someone may have declined outside the ticketing system).
    /// </summary>
    private static bool Differs(AttendanceState source, AttendanceState processed, bool strict) => source switch
    {
        AttendanceState.Attending => processed != AttendanceState.Attending,
        AttendanceState.NotAttending => processed != AttendanceState.NotAttending,
        _ => processed switch
        {
            AttendanceState.Attending => true,
            AttendanceState.NotAttending => strict,
            _ => false,
        },
    };

    private static string Describe(IEnumerable<DayComparison> days, Func<DayComparison, AttendanceState> state) =>
        string.Join('\n', days.Select(d => $"{EventDays.Get(d.Day).ShortLabel}: {state(d).ToDisplay()}"));
}
