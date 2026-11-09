using GalaReconciler.Web.Features.Reconciliation;
using GalaReconciler.Web.Features.Reconciliation.Domain;
using GalaReconciler.Web.Features.Reconciliation.Rules;
using Microsoft.Extensions.Options;
using static GalaReconciler.Tests.Helpers.TestGuests;

namespace GalaReconciler.Tests.Rules;

public class DatesMismatchRuleTests
{
    private static DatesMismatchRule Rule(bool strict = false) =>
        new(Options.Create(new ReconciliationOptions { StrictNotAttending = strict }));

    [Fact]
    public void No_discrepancy_when_days_agree()
    {
        var source = Guest("A", (EventDay.Saturday, AttendanceState.Attending));
        var processed = Guest("A", (EventDay.Saturday, AttendanceState.Attending));

        Assert.Null(Rule().Evaluate(Pair(source, processed)));
    }

    [Fact]
    public void One_discrepancy_listing_every_differing_day()
    {
        var source = Guest("A",
            (EventDay.Thursday, AttendanceState.Attending),
            (EventDay.Friday, AttendanceState.Attending),
            (EventDay.Saturday, AttendanceState.Attending));
        var processed = Guest("A",
            (EventDay.Thursday, AttendanceState.Attending),
            (EventDay.Friday, AttendanceState.NotAttending),
            (EventDay.GridGala, AttendanceState.Attending));

        var discrepancy = Rule().Evaluate(Pair(source, processed));

        Assert.NotNull(discrepancy);
        Assert.Equal(DiscrepancyType.DatesMismatch, discrepancy.Type);
        Assert.Equal([EventDay.Friday, EventDay.Saturday, EventDay.GridGala], discrepancy.AffectedDays);
        Assert.Equal(5, discrepancy.Days.Count);
        Assert.Equal("Differs on Fri, Sat, Gala", discrepancy.Detail);
        Assert.Equal("Fri: Attending\nSat: Attending\nGala: No response", discrepancy.SourceValue);
        Assert.Equal("Fri: Not attending\nSat: No response\nGala: Attending", discrepancy.ProcessedValue);
    }

    [Fact]
    public void Not_attending_and_no_response_are_equivalent_by_default()
    {
        var source = Guest("A", (EventDay.Saturday, AttendanceState.NotAttending));
        var processed = Guest("A");

        Assert.Null(Rule().Evaluate(Pair(source, processed)));
    }

    [Fact]
    public void Strict_option_flags_not_attending_versus_no_response()
    {
        var source = Guest("A", (EventDay.Saturday, AttendanceState.NotAttending));
        var processed = Guest("A");

        var discrepancy = Rule(strict: true).Evaluate(Pair(source, processed));
        Assert.NotNull(discrepancy);
        Assert.Equal([EventDay.Saturday], discrepancy.AffectedDays);
    }
}

public class MobileNumberMismatchRuleTests
{
    private readonly MobileNumberMismatchRule _rule = new();

    [Fact]
    public void No_discrepancy_when_sets_are_equal()
    {
        var source = Guest("A").WithPhones("0491570737", "0870100175");
        var processed = Guest("A").WithPhones("0870100175", "0491570737");

        Assert.Null(_rule.Evaluate(Pair(source, processed)));
    }

    [Fact]
    public void No_discrepancy_when_both_are_blank() => Assert.Null(_rule.Evaluate(Pair(Guest("A"), Guest("A"))));

    [Fact]
    public void Flags_a_value_only_in_source_and_shows_all_values()
    {
        var source = Guest("A").WithPhones("0491570156", "049157156");
        var processed = Guest("A").WithPhones("0491570156");

        var discrepancy = _rule.Evaluate(Pair(source, processed));

        Assert.NotNull(discrepancy);
        Assert.Equal(DiscrepancyType.MobileNumberMismatch, discrepancy.Type);
        Assert.Equal("Only in Source: 049157156", discrepancy.Detail);
        Assert.Equal("0491570156\n049157156", discrepancy.SourceValue);
        Assert.Equal("0491570156", discrepancy.ProcessedValue);
    }

    [Fact]
    public void Flags_values_on_both_sides()
    {
        var discrepancy = _rule.Evaluate(Pair(Guest("A").WithPhones("0491573770"), Guest("A").WithPhones("0491573087")));

        Assert.Equal("Only in Source: 0491573770; Only in Processed: 0491573087", discrepancy?.Detail);
    }

    [Theory]
    [InlineData(true, "Missing in Source")]
    [InlineData(false, "Missing in Processed")]
    public void Flags_one_blank_side(bool sourceBlank, string expected)
    {
        var withPhone = Guest("A").WithPhones("0491573770");
        var discrepancy = sourceBlank
            ? _rule.Evaluate(Pair(Guest("A"), withPhone))
            : _rule.Evaluate(Pair(withPhone, Guest("A")));

        Assert.Equal(expected, discrepancy?.Detail);
    }
}

public class EmailMismatchRuleTests
{
    private readonly EmailMismatchRule _rule = new();

    [Fact]
    public void No_discrepancy_when_sets_are_equal() =>
        Assert.Null(_rule.Evaluate(Pair(Guest("A").WithEmails("a@x.com"), Guest("A").WithEmails("a@x.com"))));

    [Fact]
    public void Flags_different_addresses()
    {
        var discrepancy = _rule.Evaluate(Pair(
            Guest("Olivia Grant Taylor").WithEmails("olivia.grant@example.com"),
            Guest("Olivia Grant-Taylor").WithEmails("ogrant@example.org")));

        Assert.NotNull(discrepancy);
        Assert.Equal(DiscrepancyType.EmailMismatch, discrepancy.Type);
        Assert.Equal("Only in Source: olivia.grant@example.com; Only in Processed: ogrant@example.org", discrepancy.Detail);
    }

    [Fact]
    public void Flags_missing_in_source()
    {
        var discrepancy = _rule.Evaluate(Pair(Guest("Ethan Cole"), Guest("Ethan Cole").WithEmails("ethan.cole@example.net")));

        Assert.Equal("Missing in Source", discrepancy?.Detail);
        Assert.Null(discrepancy?.SourceValue);
        Assert.Equal("ethan.cole@example.net", discrepancy?.ProcessedValue);
    }
}
