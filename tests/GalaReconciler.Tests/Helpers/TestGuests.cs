using GalaReconciler.Web.Features.Reconciliation.Domain;
using GalaReconciler.Web.Features.Reconciliation.Matching;
using GalaReconciler.Web.Features.Reconciliation.Normalization;

namespace GalaReconciler.Tests.Helpers;

/// <summary>Builds <see cref="GuestRecord"/>s directly for matcher and rule tests.</summary>
public static class TestGuests
{
    public static GuestRecord Guest(string displayName, params (EventDay Day, AttendanceState State)[] days)
    {
        var record = new GuestRecord { DisplayName = displayName, MatchKey = NameNormalizer.Normalize(displayName) };
        foreach (var (day, state) in days)
        {
            record.Attendance[day] = state;
        }

        return record;
    }

    public static GuestRecord WithPhones(this GuestRecord record, params string[] phones)
    {
        record.Phones.UnionWith(phones);
        return record;
    }

    public static GuestRecord WithEmails(this GuestRecord record, params string[] emails)
    {
        record.Emails.UnionWith(emails);
        return record;
    }

    public static MatchedPair Pair(GuestRecord source, GuestRecord processed) => new(source, processed, MatchMethod.Name);
}
