using GalaReconciler.Web.Features.Reconciliation.Domain;
using GalaReconciler.Web.Features.Reconciliation.Matching;
using Microsoft.Extensions.Options;

namespace GalaReconciler.Web.Features.Reconciliation.Rules;

/// <summary>
/// Flags days where one side says Attending and the other doesn't. "Not attending" and "no response"
/// both mean "not coming" unless <see cref="ReconciliationOptions.StrictNotAttending"/> is on.
/// </summary>
public sealed class DatesMismatchRule(IOptions<ReconciliationOptions> options) : IDiscrepancyRule
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

    private static bool Differs(AttendanceState source, AttendanceState processed, bool strict) =>
        strict
            ? source != processed
            : (source == AttendanceState.Attending) != (processed == AttendanceState.Attending);

    private static string Describe(IEnumerable<DayComparison> days, Func<DayComparison, AttendanceState> state) =>
        string.Join('\n', days.Select(d => $"{EventDays.Get(d.Day).ShortLabel}: {state(d).ToDisplay()}"));
}
