using ClosedXML.Excel;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Parsing;
using GalaReconciler.Tests.Helpers;

namespace GalaReconciler.Tests.Parsing;

public class SourceRsvpParserTests
{
    private static ParseResult Parse(WorkbookBuilder builder)
    {
        using var stream = builder.ToStream();
        return new SourceRsvpParser().Parse(stream);
    }

    private static ParseResult Parse(params SourceTicket[] tickets) =>
        Parse(new WorkbookBuilder().AddSourceSheet("RSVP", tickets));

    private static GuestRecord Single(ParseResult result, string displayName) =>
        Assert.Single(result.Records, r => r.DisplayName == displayName);

    [Fact]
    public void Header_map_is_case_sensitive_for_duplicate_name_headers()
    {
        using var workbook = new XLWorkbook(new WorkbookBuilder().AddSourceSheet("RSVP").ToStream());
        var map = new HeaderMap(workbook.Worksheet(1), StringComparison.Ordinal);

        Assert.Equal(2, map.Find("First name"));
        Assert.Equal(3, map.Find("Last name"));
        Assert.Equal(21, map.Find("First Name"));
        Assert.Equal(22, map.Find("Last Name"));
    }

    [Fact]
    public void Uses_attendee_name_not_ticket_holder_name()
    {
        var result = Parse(new SourceTicket("Saturday attending", "Marco", "Bellini")
        {
            TicketHolderName = ("Jane", "Assistant"),
        });

        var record = Assert.Single(result.Records);
        Assert.Equal("Marco Bellini", record.DisplayName);
        Assert.Equal("marco bellini", record.MatchKey);
    }

    [Fact]
    public void Falls_back_to_columns_B_and_C_when_attendee_name_is_blank()
    {
        var result = Parse(new SourceTicket("Saturday attending", "Alice", "Romano") { AttendeeNameBlank = true });

        Assert.Equal("Alice Romano", Assert.Single(result.Records).DisplayName);
    }

    [Fact]
    public void Groups_per_day_rows_into_one_person_with_every_distinct_phone()
    {
        var result = Parse(
            new SourceTicket("Thursday attending", "Marco", "Bellini", "Q7RT2MXA") { Telephone = "0491570156" },
            new SourceTicket("Friday attending", "Marco", "Bellini", "Q7RT2MXA") { Telephone = 491570156 },
            new SourceTicket("Saturday not attending", "Marco", "Bellini", "Q7RT2MXA") { Telephone = "049157156", Mobile = "" },
            new SourceTicket("Sunday attending", "Marco", "Bellini", "Q7RT2MXA") { Mobile = "0491 570 156" });

        var marco = Assert.Single(result.Records);
        Assert.Equal(AttendanceState.Attending, marco.GetAttendance(EventDay.Thursday));
        Assert.Equal(AttendanceState.Attending, marco.GetAttendance(EventDay.Friday));
        Assert.Equal(AttendanceState.NotAttending, marco.GetAttendance(EventDay.Saturday));
        Assert.Equal(AttendanceState.Attending, marco.GetAttendance(EventDay.Sunday));
        Assert.Equal(AttendanceState.NoResponse, marco.GetAttendance(EventDay.GridGala));
        Assert.Equal(["0491570156", "049157156"], marco.Phones.Order());
        Assert.Equal([2, 3, 4, 5], marco.Rows.Select(r => r.RowNumber));
        Assert.All(marco.Rows, r => Assert.Equal("Q7RT2MXA", r.OrderId));
    }

    [Theory]
    [InlineData("Mr", "Dr", "Dr")]       // "Please specify" wins
    [InlineData("Mr", "", "Mr")]         // nothing specified: the drop-down
    [InlineData("Other", "", null)]      // "Other" on its own means nothing was chosen
    [InlineData("", "nil", null)]
    [InlineData("Other", "Prof", "Prof")]
    public void Specified_value_wins_over_the_drop_down(string choice, string specified, string? expected) =>
        Assert.Equal(expected, SourceRsvpParser.PreferSpecified(choice, specified));

    [Fact]
    public void Reads_title_post_nominal_and_position_title()
    {
        var result = Parse(
            new SourceTicket("Saturday attending", "Henry", "Walsh")
            {
                Title = "Mr", TitleOther = "Dr", PostNominal = "AM", PostNominalOther = "AO", PositionTitle = "Director",
            },
            new SourceTicket("Saturday attending", "Clara", "Walsh") { Title = "Mrs", PostNominal = "OAM", PositionTitle = "nil" });

        var henry = Single(result, "Henry Walsh");
        Assert.Equal("Dr", henry.Title);
        Assert.Equal("AO", henry.PostNominal);
        Assert.Equal("Director", henry.Position);

        var clara = Single(result, "Clara Walsh");
        Assert.Equal("Mrs", clara.Title);
        Assert.Equal("OAM", clara.PostNominal);
        Assert.Null(clara.Position);
    }

    [Fact]
    public void Ignores_buyer_contact_details()
    {
        var result = Parse(new SourceTicket("Saturday not attending", "Ethan", "Cole")
        {
            BuyerFirstName = "Nina", BuyerLastName = "Patel", BuyerMobile = "0491575789", BuyerEmail = "nina@x.com",
        });

        var ethan = Assert.Single(result.Records);
        Assert.Empty(ethan.Phones);
        Assert.Empty(ethan.Emails);
        Assert.Null(ethan.HostDisplayName); // the buyer isn't attending, so isn't a host
    }

    [Fact]
    public void Unknown_ticket_type_produces_a_warning_and_does_not_throw()
    {
        var result = Parse(
            new SourceTicket("Friday not attending", "Sam", "Fletcher"),
            new SourceTicket("Sunday waitlist", "Sam", "Fletcher"));

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("Sunday waitlist", warning.Message);
        Assert.Contains("row 3", warning.Message);
        var sam = Assert.Single(result.Records);
        Assert.Equal(AttendanceState.NotAttending, sam.GetAttendance(EventDay.Friday));
        Assert.Equal(AttendanceState.NoResponse, sam.GetAttendance(EventDay.Sunday));
    }

    [Fact]
    public void Conflicting_states_for_one_day_are_attending_with_a_warning()
    {
        var result = Parse(
            new SourceTicket("Saturday not attending", "Alice", "Romano", "A1"),
            new SourceTicket("Saturday attending", "Alice", "Romano", "A2"));

        Assert.Equal(AttendanceState.Attending, Assert.Single(result.Records).GetAttendance(EventDay.Saturday));
        Assert.Contains(result.Warnings, w => w.Message.Contains("both attending and not attending"));
    }

    [Fact]
    public void Extra_same_day_ticket_on_one_order_is_an_unnamed_guest()
    {
        var result = Parse(
            new SourceTicket("Sunday not attending", "Luca", "Di Marco", "ZK4W8NPL") { Email = "luca@x.com" },
            new SourceTicket("Sunday not attending", "Luca", "Di Marco", "ZK4W8NPL") { Email = "luca@x.com" });

        Assert.Equal(2, result.Records.Count);
        var luca = Single(result, "Luca Di Marco");
        var guest = Single(result, "Unnamed guest of Luca Di Marco");

        Assert.True(guest.IsUnnamedGuest);
        Assert.Equal("Luca Di Marco", guest.HostDisplayName);
        Assert.Equal(AttendanceState.NotAttending, guest.GetAttendance(EventDay.Sunday));
        Assert.Empty(guest.Emails);
        Assert.Equal(["luca@x.com"], luca.Emails);
        Assert.Equal(2, Assert.Single(luca.Rows).RowNumber);
        Assert.Equal(3, Assert.Single(guest.Rows).RowNumber);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Attendee_bought_for_by_another_attendee_is_their_guest()
    {
        var result = Parse(
            new SourceTicket("Saturday attending", "Marco", "Bellini", "Q7RT2MXA"),
            new SourceTicket("Saturday attending", "Leilani", "Fonua", "Q7RT2MXA") { BuyerFirstName = "Marco", BuyerLastName = "Bellini" });

        Assert.Null(Single(result, "Marco Bellini").HostDisplayName);
        Assert.Equal("Marco Bellini", Single(result, "Leilani Fonua").HostDisplayName);
    }

    [Fact]
    public void Reads_every_sheet()
    {
        var result = Parse(new WorkbookBuilder()
            .AddSourceSheet("Thurs_Sun Suite RSVP", new SourceTicket("Saturday attending", "Oscar", "Reid", "A"))
            .AddSourceSheet("Grid Gala RSVP", new SourceTicket("Grid Gala attending", "Oscar", "Reid", "B")));

        var oscar = Assert.Single(result.Records);
        Assert.Equal(AttendanceState.Attending, oscar.GetAttendance(EventDay.Saturday));
        Assert.Equal(AttendanceState.Attending, oscar.GetAttendance(EventDay.GridGala));
        Assert.Equal(["Grid Gala RSVP", "Thurs_Sun Suite RSVP"], oscar.Rows.Select(r => r.SheetName).Order());
    }

    [Fact]
    public void Same_day_on_two_orders_is_flagged_as_a_duplicate()
    {
        var result = Parse(
            new SourceTicket("Saturday attending", "Alice", "Romano", "A1"),
            new SourceTicket("Saturday attending", "Alice", "Romano", "A2"));

        Assert.Single(result.Records);
        Assert.Contains(result.Warnings, w => w.Message.Contains("more than one order"));
    }

    [Fact]
    public void Missing_required_header_throws_a_friendly_error()
    {
        var headers = SourceSheet.Headers.Where(h => h is not "Ticket type" and not "Email").ToList();
        var builder = new WorkbookBuilder().Sheet("RSVP", headers, [["GALA", "Marco"]]);

        // Without "Ticket type" the sheet still has "Order id", so it's treated as an RSVP sheet with gaps.
        var ex = Assert.Throws<WorkbookFormatException>(() => Parse(builder));
        Assert.Equal(SourceRsvpParser.FileLabel, ex.FileLabel);
        Assert.Equal(["Ticket type", "Email"], ex.MissingHeaders);
        Assert.Contains("RSVP", ex.Message);
    }

    [Fact]
    public void Not_a_workbook_throws_a_friendly_error()
    {
        using var stream = new MemoryStream("Event,First name\n"u8.ToArray());
        var ex = Assert.Throws<WorkbookFormatException>(() => new SourceRsvpParser().Parse(stream));
        Assert.Contains("valid .xlsx", ex.Message);
    }
}
