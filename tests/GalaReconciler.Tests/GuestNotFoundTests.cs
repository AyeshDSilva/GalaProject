using GalaReconciler.Core;
using GalaReconciler.Core.Domain;
using GalaReconciler.Tests.Helpers;

namespace GalaReconciler.Tests;

/// <summary>People who are really in the files must not come out as "Guest not found".</summary>
public class GuestNotFoundTests
{
    private static ReconciliationResult Compare(SourceTicket[] main, SourceTicket[] gala, params ProcessedGuest[] master)
    {
        using var source = new WorkbookBuilder().AddSourceSheet("RSVP", main).ToStream();
        using var galaFile = new WorkbookBuilder().AddGalaSheet("attendee-report", gala).ToStream();
        using var processed = new WorkbookBuilder().AddProcessedSheet("Sheet1", master).ToStream();
        return TestHandlers.CreateConsoleCompareFilesHandler().Handle(
            new CompareFilesRequest(source, "s", processed, "p", galaFile, "g", MainGuestsOnlyInNotFound: true));
    }

    private static List<Discrepancy> NotFound(ReconciliationResult result) =>
        result.Discrepancies.Where(d => d.Type == DiscrepancyType.GuestNotFound).ToList();

    [Fact]
    public void Same_person_spelled_differently_in_the_two_exports_is_one_person()
    {
        var result = Compare(
            [new SourceTicket("Saturday attending", "Will", "Hartley", "O1") { Email = "w@example.com" }],
            [new SourceTicket("RSVP - Attending", "William", "Hartley", "G1") { Email = "w@example.com" }],
            new ProcessedGuest("William", "Hartley") { Sat = "Y", GridGala = "Y", Email1 = "w@example.com" });

        Assert.Empty(result.Discrepancies);
        Assert.Equal(1, result.SourceGuestCount);
        Assert.Contains(result.Warnings, w => w.Message.Contains("treated as one person"));
    }

    [Fact]
    public void Spelled_differently_in_both_exports_and_the_master_list_still_matches_by_email()
    {
        var result = Compare(
            [new SourceTicket("Saturday attending", "Bill", "Hartley", "O1") { Email = "w@example.com" }],
            [new SourceTicket("RSVP - Attending", "Will", "Hartley", "G1") { Email = "w@example.com" }],
            new ProcessedGuest("William", "Hartley") { Sat = "Y", GridGala = "Y", Email1 = "w@example.com" });

        Assert.Empty(NotFound(result));
    }

    [Fact]
    public void A_couple_sharing_an_email_across_the_exports_stays_two_people()
    {
        var result = Compare(
            [new SourceTicket("Saturday attending", "Steve", "Dorman", "O1") { Email = "home@example.com" }],
            [new SourceTicket("RSVP - Attending", "Fiona", "Dorman", "G1") { Email = "home@example.com" }],
            new ProcessedGuest("Steve", "Dorman") { Sat = "Y", Email1 = "home@example.com" },
            new ProcessedGuest("Fiona", "Dorman") { GridGala = "Y", Email1 = "home@example.com" });

        Assert.Empty(result.Discrepancies);
        Assert.Equal(2, result.SourceGuestCount);
    }

    [Fact]
    public void Extra_ticket_under_the_hosts_name_pairs_with_the_guest_named_in_the_master_list()
    {
        var result = Compare(
            [
                new SourceTicket("Sunday not attending", "Joe", "De Gennaro", "O1"),
                new SourceTicket("Sunday not attending", "Joe", "De Gennaro", "O1"),
            ],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Joe", "De Gennaro") { Sun = "N", GuestName = "Mrs Ann De Gennaro" },
            new ProcessedGuest("Ann", "De Gennaro")
            {
                Sun = "N", Position = "Guest of Mr Joe De Gennaro", Phone = "0491 570 111", Email1 = "ann@example.com",
            },
            new ProcessedGuest("Other", "Person") { GridGala = "Y" });

        Assert.Empty(result.Discrepancies);
    }

    [Fact]
    public void Extra_ticket_is_still_checked_against_the_named_guests_days()
    {
        var result = Compare(
            [
                new SourceTicket("Sunday attending", "Joe", "De Gennaro", "O1"),
                new SourceTicket("Sunday attending", "Joe", "De Gennaro", "O1"),
            ],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Joe", "De Gennaro") { Sun = "Y", GuestName = "Mrs Ann De Gennaro" },
            new ProcessedGuest("Ann", "De Gennaro") { Sun = "N", Position = "Guest of Mr Joe De Gennaro" },
            new ProcessedGuest("Other", "Person") { GridGala = "Y" });

        var dates = Assert.Single(result.Discrepancies);
        Assert.Equal(DiscrepancyType.DatesMismatch, dates.Type);
        Assert.Equal("Ann De Gennaro", dates.GuestDisplayName);
    }

    [Fact]
    public void Name_in_columns_B_and_C_counts_when_the_attendee_name_is_spelled_differently()
    {
        var result = Compare(
            [new SourceTicket("Saturday attending", "Jimmy", "Whaley", "O1") { TicketHolderName = ("Jim", "Whalley") }],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Jim", "Whalley") { Sat = "Y" },
            new ProcessedGuest("Other", "Person") { GridGala = "Y" });

        Assert.Empty(NotFound(result));
    }

    [Fact]
    public void An_exact_attendee_name_beats_someone_elses_ticket_holder_name()
    {
        var result = Compare(
            [
                new SourceTicket("Saturday attending", "Marco", "Bellini", "O1") { TicketHolderName = ("Jane", "Assistant") },
                new SourceTicket("Saturday attending", "Jane", "Assistant", "O2"),
            ],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Marco", "Bellini") { Sat = "Y" },
            new ProcessedGuest("Jane", "Assistant") { Sat = "Y" },
            new ProcessedGuest("Other", "Person") { GridGala = "Y" });

        Assert.Empty(result.Discrepancies);
    }

    [Theory]
    [InlineData(true, false, "Only in RSVP export")]
    [InlineData(false, true, "Only in Gala RSVP export")]
    [InlineData(true, true, "In both RSVP exports, not in the master list")]
    public void Says_which_export_someone_was_found_in(bool inMain, bool inGala, string expected)
    {
        var someone = new SourceTicket("Saturday attending", "Sam", "Fletcher", "O1");
        var galaTicket = someone with { TicketType = "RSVP - Attending", OrderId = "G1" };

        var result = Compare(
            inMain ? [someone] : [new SourceTicket("Saturday attending", "Other", "Person", "O9")],
            inGala ? [galaTicket] : [new SourceTicket("RSVP - Attending", "Other", "Person", "G9")],
            new ProcessedGuest("Other", "Person") { Sat = "Y", GridGala = "Y" });

        var notFound = NotFound(result).Single(d => d.GuestDisplayName == "Sam Fletcher");
        Assert.Equal(expected, notFound.Detail);
    }

    [Fact]
    public void Says_when_someone_is_in_neither_export()
    {
        var result = Compare(
            [new SourceTicket("Saturday attending", "Other", "Person", "O9")],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G9")],
            new ProcessedGuest("Other", "Person") { Sat = "Y", GridGala = "Y" },
            new ProcessedGuest("Maeve", "Connell") { GridGala = "Y" });

        Assert.Equal("Not in either RSVP export", Assert.Single(NotFound(result)).Detail);
    }

    [Fact]
    public void Someone_with_their_own_ticket_is_found_in_the_guest_name_column()
    {
        // Bought their own ticket, so the export doesn't say they're a guest; the master list only names them here.
        var result = Compare(
            [new SourceTicket("Saturday attending", "Tharushi", "Ediriweera", "O1") { Title = "Mrs", Mobile = "0491 570 111", Email = "t@example.com", PositionTitle = "Manager" }],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Marco", "Bellini") { Sat = "Y", GuestName = "Mrs Tharushi Ediriweera" },
            new ProcessedGuest("Other", "Person") { GridGala = "Y" });

        // Found, and nothing else is flagged: there's no row of hers to compare with. Marco has no ticket and is the only not-found.
        Assert.Equal("Marco Bellini", Assert.Single(result.Discrepancies).GuestDisplayName);
        Assert.Equal(DiscrepancyType.GuestNotFound, Assert.Single(result.Discrepancies).Type);
    }

    [Fact]
    public void Guest_name_entries_with_no_ticket_are_not_reported()
    {
        var result = Compare(
            [new SourceTicket("Saturday attending", "Marco", "Bellini", "O1")],
            [new SourceTicket("RSVP - Attending", "Marco", "Bellini", "G1")],
            new ProcessedGuest("Marco", "Bellini") { Sat = "Y", GridGala = "Y", GuestName = "Mrs Tharushi Ediriweera" });

        Assert.Empty(result.Discrepancies);
    }

    [Fact]
    public void Web_registration_also_finds_people_in_the_guest_name_column_without_reporting_the_rest()
    {
        using var source = new WorkbookBuilder().AddSourceSheet("RSVP", new SourceTicket("Saturday attending", "Tharushi", "Ediriweera", "O1")).ToStream();
        using var processed = new WorkbookBuilder().AddProcessedSheet(
            "Sheet1",
            new ProcessedGuest("Marco", "Bellini") { GuestName = "Mrs Tharushi Ediriweera\nMr Nobody Here" }).ToStream();

        var result = TestHandlers.CreateCompareFilesHandler().Handle(new CompareFilesRequest(source, "s", processed, "p"));

        Assert.DoesNotContain(result.Discrepancies, d => d.GuestDisplayName is "Tharushi Ediriweera" or "Nobody Here");
    }

    [Fact]
    public void An_extra_ticket_pairs_with_a_name_in_the_guest_name_column()
    {
        var result = Compare(
            [
                new SourceTicket("Saturday attending", "Marco", "Bellini", "O1"),
                new SourceTicket("Saturday attending", "Marco", "Bellini", "O1"),
            ],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Marco", "Bellini") { Sat = "Y", GuestName = "Mrs Tharushi Ediriweera" },
            new ProcessedGuest("Other", "Person") { GridGala = "Y" });

        Assert.Empty(result.Discrepancies);
    }

    [Fact]
    public void Guests_of_someone_are_left_out_on_both_sides()
    {
        var result = Compare(
            [
                new SourceTicket("Saturday attending", "Host", "Person", "O1"),
                new SourceTicket("Saturday attending", "Bought", "Forguest", "O1") { BuyerFirstName = "Host", BuyerLastName = "Person" },
                new SourceTicket("Sunday attending", "Host", "Person", "O2"),
                new SourceTicket("Sunday attending", "Host", "Person", "O2"),
            ],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Host", "Person") { Sat = "Y", Sun = "Y" },
            new ProcessedGuest("Listed", "Guest") { Sat = "Y", Position = "Guest of Mr Other Person" },
            new ProcessedGuest("Other", "Person") { GridGala = "Y", GuestName = "Mr Named Guest" },
            new ProcessedGuest("Named", "Guest") { GridGala = "Y" });

        // "Bought Forguest" and the extra ticket on the export side; "Listed Guest" and "Named Guest" on the master list.
        Assert.Empty(NotFound(result));
    }

    [Fact]
    public void Main_guests_are_still_reported_on_both_sides()
    {
        var result = Compare(
            [new SourceTicket("Saturday attending", "Sam", "Fletcher", "O1")],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Other", "Person") { GridGala = "Y" },
            new ProcessedGuest("Ruby", "McIntyre") { Sat = "Y" });

        Assert.Equal(["Ruby McIntyre", "Sam Fletcher"], NotFound(result).Select(d => d.GuestDisplayName).Order());
    }

    [Fact]
    public void A_found_guest_is_still_compared()
    {
        var result = Compare(
            [
                new SourceTicket("Saturday attending", "Host", "Person", "O1"),
                new SourceTicket("Saturday attending", "Clara", "Walsh", "O1") { BuyerFirstName = "Host", BuyerLastName = "Person" },
            ],
            [new SourceTicket("RSVP - Attending", "Other", "Person", "G1")],
            new ProcessedGuest("Host", "Person") { Sat = "Y", GuestName = "Ms Clara Walsh" },
            new ProcessedGuest("Clara", "Walsh") { Sat = "N", Position = "Guest of Mr Host Person" },
            new ProcessedGuest("Other", "Person") { GridGala = "Y" });

        Assert.Equal(DiscrepancyType.DatesMismatch, Assert.Single(result.Discrepancies).Type);
    }

    [Fact]
    public void Without_the_setting_guests_are_still_reported()
    {
        using var source = new WorkbookBuilder().AddSourceSheet("RSVP", new SourceTicket("Saturday attending", "Host", "Person", "O1")).ToStream();
        using var processed = new WorkbookBuilder().AddProcessedSheet(
            "Sheet1",
            new ProcessedGuest("Host", "Person") { Sat = "Y", GuestName = "Mr Named Guest" },
            new ProcessedGuest("Named", "Guest") { Sat = "Y", Position = "Guest of Mr Host Person" }).ToStream();

        var result = TestHandlers.CreateCompareFilesHandler().Handle(new CompareFilesRequest(source, "s", processed, "p"));

        Assert.Equal("Named Guest", Assert.Single(NotFound(result)).GuestDisplayName);
    }
}
