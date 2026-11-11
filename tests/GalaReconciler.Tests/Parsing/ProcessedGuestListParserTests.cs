using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Parsing;
using GalaReconciler.Tests.Helpers;

namespace GalaReconciler.Tests.Parsing;

public class ProcessedGuestListParserTests
{
    private static ParseResult Parse(params ProcessedGuest?[] guests)
    {
        using var stream = new WorkbookBuilder().AddProcessedSheet("Sheet1", guests).ToStream();
        return new ProcessedGuestListParser().Parse(stream);
    }

    private static GuestRecord Single(ParseResult result, string displayName) =>
        Assert.Single(result.Records, r => r.DisplayName == displayName);

    [Theory]
    [InlineData("Y", AttendanceState.Attending)]
    [InlineData(" y ", AttendanceState.Attending)]
    [InlineData("1", AttendanceState.AwaitingResponse)]
    [InlineData("N", AttendanceState.NotAttending)]
    [InlineData("n", AttendanceState.NotAttending)]
    [InlineData("", AttendanceState.NotInvited)]
    [InlineData(null, AttendanceState.NotInvited)]
    public void Parses_day_cells(string? value, AttendanceState expected)
    {
        Assert.True(ProcessedGuestListParser.TryParseDayCell(value, out var state));
        Assert.Equal(expected, state);
    }

    [Fact]
    public void Finds_columns_by_header_despite_hidden_columns_and_blank_column_A()
    {
        var result = Parse(new ProcessedGuest("Alice", "Romano")
        {
            Prenom = "Ms", Sat = "Y", GridGala = 1, Thurs = "N", Phone = "0491 570 159", Email1 = "Alice.Romano@Example.com",
        });

        var alice = Assert.Single(result.Records);
        Assert.Equal("Alice Romano", alice.DisplayName);
        Assert.Equal("Ms", alice.Title);
        Assert.Equal(AttendanceState.NotAttending, alice.GetAttendance(EventDay.Thursday));
        Assert.Equal(AttendanceState.NotInvited, alice.GetAttendance(EventDay.Friday));
        Assert.Equal(AttendanceState.Attending, alice.GetAttendance(EventDay.Saturday));
        Assert.Equal(AttendanceState.AwaitingResponse, alice.GetAttendance(EventDay.GridGala));
        Assert.Equal(["0491570159"], alice.Phones);
        Assert.Equal(["alice.romano@example.com"], alice.Emails);
        Assert.Equal(new RowReference("Sheet1", 2), Assert.Single(alice.Rows));
    }

    [Fact]
    public void Reads_multi_line_phone_numeric_phone_and_both_email_columns()
    {
        var result = Parse(
            new ProcessedGuest("Oscar", "Reid") { Phone = "- 0491570737\n08 7010 0175", Email1 = "oscar@x.com", Email2 = "oscar@y.com" },
            new ProcessedGuest("Olivia", "Grant-Taylor") { Phone = 491570110 });

        Assert.Equal(["0491570737", "0870100175"], Single(result, "Oscar Reid").Phones.Order());
        Assert.Equal(["oscar@x.com", "oscar@y.com"], Single(result, "Oscar Reid").Emails.Order());
        Assert.Equal(["0491570110"], Single(result, "Olivia Grant-Taylor").Phones);
    }

    [Fact]
    public void Skips_blank_rows()
    {
        var result = Parse(new ProcessedGuest("A", "One"), null, null, new ProcessedGuest("B", "Two"));

        Assert.Equal(["A One", "B Two"], result.Records.Select(r => r.DisplayName));
        Assert.Equal(5, Single(result, "B Two").Rows[0].RowNumber);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parses_host_from_position_including_doubled_title()
    {
        var result = Parse(
            new ProcessedGuest("Oscar", "Reid") { GuestName = "Ms Maeve O’Connell" },
            new ProcessedGuest("Maeve", "O'Connell") { Position = "Guest of Mr Mr Oscar Reid" });

        var maeve = Single(result, "Maeve O'Connell");
        Assert.Equal("Oscar Reid", maeve.HostDisplayName);
        Assert.Equal("oscar reid", maeve.HostMatchKey);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Guest_name_column_links_guests_without_a_position()
    {
        var result = Parse(
            new ProcessedGuest("Henry", "Walsh") { GuestName = "Mrs Clara Walsh\nMr Felix Moreau" },
            new ProcessedGuest("Clara", "Walsh"),
            new ProcessedGuest("Felix", "Moreau") { Position = "Guest of Mr Henry Walsh" });

        Assert.Equal("Henry Walsh", Single(result, "Clara Walsh").HostDisplayName);
        Assert.Equal("Henry Walsh", Single(result, "Felix Moreau").HostDisplayName);
    }

    [Fact]
    public void Guest_named_guest_becomes_an_unnamed_guest_with_the_hosts_days()
    {
        var result = Parse(new ProcessedGuest("Luca", "Di Marco") { Sun = "N", Sat = "Y", GuestName = "guest", Email1 = "luca@x.com" });

        var guest = Single(result, "Unnamed guest of Luca Di Marco");
        Assert.True(guest.IsUnnamedGuest);
        Assert.Equal("Luca Di Marco", guest.HostDisplayName);
        Assert.Equal(AttendanceState.NotAttending, guest.GetAttendance(EventDay.Sunday));
        Assert.Equal(AttendanceState.Attending, guest.GetAttendance(EventDay.Saturday));
        Assert.Empty(guest.Emails);
        Assert.Equal(2, Assert.Single(guest.Rows).RowNumber);
    }

    [Fact]
    public void Warns_about_inconsistent_guest_links()
    {
        var result = Parse(
            new ProcessedGuest("Marco", "Bellini") { GuestName = "Ms Leilani Fonua" },
            new ProcessedGuest("Leilani", "Fonua") { Position = "Guest of Mr Someone Else" });

        Assert.Contains(result.Warnings, w => w.Message.Contains("Someone Else"));
    }

    [Fact]
    public void A_name_only_in_the_guest_name_column_is_a_guest_of_that_host()
    {
        var result = Parse(new ProcessedGuest("Marco", "Bellini") { GuestName = "Mrs Tharushi Ediriweera" });

        var guest = Assert.Single(result.Records, r => r.IsListedGuestOnly);
        Assert.Equal("Tharushi Ediriweera", guest.DisplayName);
        Assert.Equal("tharushi ediriweera", guest.MatchKey);
        Assert.Equal("marco bellini", guest.HostMatchKey);
        Assert.Equal("Marco Bellini", guest.HostDisplayName);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("Mrs Ann Lee\nMr Tom Lee")]
    [InlineData("Mrs Ann Lee, Mr Tom Lee")]
    [InlineData("Mrs Ann Lee; Mr Tom Lee")]
    [InlineData("Mrs Ann Lee & Mr Tom Lee")]
    [InlineData("Mrs Ann Lee and Mr Tom Lee")]
    public void One_guest_name_cell_can_hold_several_guests(string cell)
    {
        var result = Parse(new ProcessedGuest("Marco", "Bellini") { GuestName = cell });

        Assert.Equal(["ann lee", "tom lee"], result.Records.Where(r => r.IsListedGuestOnly).Select(r => r.MatchKey));
    }

    [Theory]
    [InlineData("nil")]
    [InlineData("-")]
    [InlineData("TBC")]
    public void Placeholders_in_guest_name_are_not_guests(string cell)
    {
        var result = Parse(new ProcessedGuest("Marco", "Bellini") { GuestName = cell });

        Assert.DoesNotContain(result.Records, r => r.IsListedGuestOnly);
    }

    [Fact]
    public void A_guest_with_their_own_row_keeps_that_row()
    {
        var result = Parse(
            new ProcessedGuest("Marco", "Bellini") { GuestName = "Ms Leilani Fonua" },
            new ProcessedGuest("Leilani", "Fonua") { Position = "Guest of Mr Marco Bellini" });

        Assert.DoesNotContain(result.Records, r => r.IsListedGuestOnly);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public void Duplicate_names_are_combined_with_a_warning()
    {
        var result = Parse(
            new ProcessedGuest("Alice", "Romano") { Sat = "Y", Email1 = "a@x.com" },
            new ProcessedGuest("Alice", "Romano") { Sun = "Y", Email1 = "b@x.com" });

        var alice = Assert.Single(result.Records);
        Assert.Equal(AttendanceState.Attending, alice.GetAttendance(EventDay.Saturday));
        Assert.Equal(AttendanceState.Attending, alice.GetAttendance(EventDay.Sunday));
        Assert.Equal(2, alice.Emails.Count);
        Assert.Contains(result.Warnings, w => w.Message.Contains("more than once"));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("maybe")]
    [InlineData("x")]
    public void Anything_else_in_a_day_cell_means_not_invited_and_is_flagged(string value)
    {
        Assert.False(ProcessedGuestListParser.TryParseDayCell(value, out var state));
        Assert.Equal(AttendanceState.NotInvited, state);
    }

    [Fact]
    public void Reads_title_post_nominal_and_position()
    {
        var result = Parse(
            new ProcessedGuest("Henry", "Walsh") { Prenom = "Dr", Postnom = "AM", Position = "Director" },
            new ProcessedGuest("Clara", "Walsh") { Prenom = "nil", Position = "Guest of Dr Henry Walsh" });

        var henry = Single(result, "Henry Walsh");
        Assert.Equal("Dr", henry.Title);
        Assert.Equal("AM", henry.PostNominal);
        Assert.Equal("Director", henry.Position);

        var clara = Single(result, "Clara Walsh");
        Assert.Null(clara.Title);
        Assert.True(ProcessedGuestListParser.IsHostLink(clara.Position));
        Assert.False(ProcessedGuestListParser.IsHostLink(henry.Position));
    }

    [Fact]
    public void Unrecognised_day_value_produces_a_warning()
    {
        var result = Parse(new ProcessedGuest("Alice", "Romano") { Sat = "maybe" });

        Assert.Equal(AttendanceState.NotInvited, Assert.Single(result.Records).GetAttendance(EventDay.Saturday));
        Assert.Contains(result.Warnings, w => w.Message.Contains("maybe"));
    }

    [Fact]
    public void Missing_required_headers_throw_a_friendly_error()
    {
        var headers = ProcessedSheet.Headers.Where(h => h is not "SUN" and not "Phone").ToList();
        using var stream = new WorkbookBuilder().Sheet("Sheet1", headers, []).ToStream();

        var ex = Assert.Throws<WorkbookFormatException>(() => new ProcessedGuestListParser().Parse(stream));
        Assert.Equal(ProcessedGuestListParser.FileLabel, ex.FileLabel);
        Assert.Equal(["SUN", "Phone"], ex.MissingHeaders);
    }
}
