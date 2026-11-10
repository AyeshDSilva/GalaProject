using GalaReconciler.Core;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Rules;
using Microsoft.Extensions.Options;
using static GalaReconciler.Tests.Helpers.TestGuests;

namespace GalaReconciler.Tests.Rules;

public class DatesMismatchRuleTests
{
    private static DatesMismatchRule Rule(bool strict = false) =>
        new(Options.Create(new ComparisonOptions { StrictNotAttending = strict }));

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
            (EventDay.Saturday, AttendanceState.NotInvited),
            (EventDay.Sunday, AttendanceState.NotInvited),
            (EventDay.GridGala, AttendanceState.Attending));

        var discrepancy = Rule().Evaluate(Pair(source, processed));

        Assert.NotNull(discrepancy);
        Assert.Equal(DiscrepancyType.DatesMismatch, discrepancy.Type);
        Assert.Equal([EventDay.Friday, EventDay.Saturday, EventDay.GridGala], discrepancy.AffectedDays);
        Assert.Equal(5, discrepancy.Days.Count);
        Assert.Equal("Differs on Fri, Sat, Gala", discrepancy.Detail);
        Assert.Equal("Fri: Attending\nSat: Attending\nGala: No RSVP", discrepancy.SourceValue);
        Assert.Equal("Fri: Not attending\nSat: Not invited\nGala: Attending", discrepancy.ProcessedValue);
    }

    /// <summary>
    /// Master list: Y = Attending, N = NotAttending, 1 = AwaitingResponse, blank = NotInvited.
    /// Export: a ticket "attending" / "not attending", or no ticket (NoResponse).
    /// </summary>
    [Theory]
    [InlineData(AttendanceState.Attending, AttendanceState.Attending, false)]
    [InlineData(AttendanceState.Attending, AttendanceState.NotAttending, true)]
    [InlineData(AttendanceState.Attending, AttendanceState.AwaitingResponse, true)] // coming, but still marked 1
    [InlineData(AttendanceState.Attending, AttendanceState.NotInvited, true)]
    [InlineData(AttendanceState.NotAttending, AttendanceState.NotAttending, false)]
    [InlineData(AttendanceState.NotAttending, AttendanceState.AwaitingResponse, true)] // declined, but still marked 1
    [InlineData(AttendanceState.NotAttending, AttendanceState.NotInvited, true)]
    [InlineData(AttendanceState.NotAttending, AttendanceState.Attending, true)]
    [InlineData(AttendanceState.NoResponse, AttendanceState.NotInvited, false)]
    [InlineData(AttendanceState.NoResponse, AttendanceState.AwaitingResponse, false)]  // no ticket yet = no response yet
    [InlineData(AttendanceState.NoResponse, AttendanceState.Attending, true)]
    [InlineData(AttendanceState.NoResponse, AttendanceState.NotAttending, false)]     // may have declined outside ticketing
    public void Compares_each_day_by_the_master_list_meaning(AttendanceState source, AttendanceState processed, bool differs)
    {
        var discrepancy = Rule().Evaluate(Pair(Guest("A", (EventDay.Saturday, source)), Guest("A", (EventDay.Saturday, processed))));

        Assert.Equal(differs, discrepancy is not null);
    }

    [Fact]
    public void Strict_option_flags_no_ticket_against_not_attending()
    {
        var source = Guest("A");
        var processed = Guest("A", (EventDay.Saturday, AttendanceState.NotAttending));

        var discrepancy = Rule(strict: true).Evaluate(Pair(source, processed));
        Assert.NotNull(discrepancy);
        Assert.Equal([EventDay.Saturday], discrepancy.AffectedDays);
    }
}

public class TextMismatchRuleTests
{
    private static GuestRecord With(string name, string? title = null, string? postNominal = null, string? position = null)
    {
        var record = Guest(name);
        record.Title = title;
        record.PostNominal = postNominal;
        record.Position = position;
        return record;
    }

    [Theory]
    [InlineData("Dr", "Ms", true)]
    [InlineData("Mr.", "Mr", false)]   // full stops and case don't count
    [InlineData("MR", "mr", false)]
    [InlineData("Dr", null, true)]     // missing in the master list
    [InlineData(null, "Ms", false)]    // blank in the export: leave it
    [InlineData("nil", "Ms", false)]
    public void Title(string? source, string? processed, bool differs)
    {
        var discrepancy = new TitleMismatchRule().Evaluate(Pair(With("A", title: source), With("A", title: processed)));

        Assert.Equal(differs, discrepancy is not null);
        if (discrepancy is not null)
        {
            Assert.Equal(DiscrepancyType.TitleMismatch, discrepancy.Type);
            Assert.Equal(source, discrepancy.SourceValue);
            Assert.Equal(processed, discrepancy.ProcessedValue);
        }
    }

    [Theory]
    [InlineData("OAM", "AM", true)]
    [InlineData("O.A.M.", "OAM", false)]
    [InlineData(null, "AM", false)]
    public void Post_nominal(string? source, string? processed, bool differs) =>
        Assert.Equal(differs, new PostNominalMismatchRule().Evaluate(Pair(With("A", postNominal: source), With("A", postNominal: processed))) is not null);

    [Theory]
    [InlineData("Managing Director", "Director", true)]
    [InlineData("Managing  director", "Managing Director", false)]
    [InlineData("nil", "Director", false)]                        // nil in the export: don't change it
    [InlineData(null, "Director", false)]
    [InlineData("Teacher", "Guest of Mr Marco Bellini", false)]   // a host link, not a job title
    [InlineData("Teacher", null, true)]
    public void Position(string? source, string? processed, bool differs) =>
        Assert.Equal(differs, new PositionMismatchRule().Evaluate(Pair(With("A", position: source), With("A", position: processed))) is not null);

    [Fact]
    public void Detail_names_both_values()
    {
        var discrepancy = new PositionMismatchRule().Evaluate(Pair(With("A", position: "Managing Director"), With("A", position: "Director")));

        Assert.Equal("Source has “Managing Director”, Processed has “Director”", discrepancy?.Detail);
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
    public void One_number_in_common_is_enough()
    {
        // Two numbers entered on separate lines of one Phone cell; one is right.
        Assert.Null(_rule.Evaluate(Pair(Guest("A").WithPhones("0491570156"), Guest("A").WithPhones("0491570156", "0870100999"))));

        // A typo on one export row doesn't matter while another row's number matches.
        Assert.Null(_rule.Evaluate(Pair(Guest("A").WithPhones("0491570156", "049157156"), Guest("A").WithPhones("0491570156"))));
    }

    [Fact]
    public void Flags_when_no_number_is_in_common_and_shows_them_all()
    {
        var discrepancy = _rule.Evaluate(Pair(
            Guest("A").WithPhones("0491574632"),
            Guest("A").WithPhones("0491576398", "0491576801")));

        Assert.NotNull(discrepancy);
        Assert.Equal(DiscrepancyType.MobileNumberMismatch, discrepancy.Type);
        Assert.Equal("Nothing in common", discrepancy.Detail);
        Assert.Equal("0491574632", discrepancy.SourceValue);
        Assert.Equal("0491576398\n0491576801", discrepancy.ProcessedValue);
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
    public void Address_in_either_email_column_is_enough()
    {
        // The master list's Email 1 and Email 2 are read into one set.
        Assert.Null(_rule.Evaluate(Pair(
            Guest("A").WithEmails("luca.dimarco@example.com"),
            Guest("A").WithEmails("luca@dimarco.example.org", "luca.dimarco@example.com"))));
    }

    [Fact]
    public void Flags_different_addresses()
    {
        var discrepancy = _rule.Evaluate(Pair(
            Guest("Olivia Grant Taylor").WithEmails("olivia.grant@example.com"),
            Guest("Olivia Grant-Taylor").WithEmails("ogrant@example.org")));

        Assert.NotNull(discrepancy);
        Assert.Equal(DiscrepancyType.EmailMismatch, discrepancy.Type);
        Assert.Equal("Nothing in common", discrepancy.Detail);
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
