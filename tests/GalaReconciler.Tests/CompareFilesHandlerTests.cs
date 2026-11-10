using GalaReconciler.Core;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;
using GalaReconciler.Tests.Helpers;
using GalaReconciler.Web.Features.Reconciliation;

namespace GalaReconciler.Tests;

/// <summary>End to end over the sample workbooks: parse → match → rules.</summary>
public class CompareFilesHandlerTests
{
    private static readonly ReconciliationResult Result = TestHandlers.CompareSamples();

    private static List<Discrepancy> For(string guest) =>
        Result.Discrepancies.Where(d => d.GuestDisplayName == guest).ToList();

    private static DiscrepancyType[] TypesFor(string guest) => For(guest).Select(d => d.Type).ToArray();

    [Fact]
    public void Marco_has_no_discrepancies_because_one_of_his_numbers_matches()
    {
        // His export rows have 0491570156 and a typo, 049157156; the master list has 0491570156.
        Assert.Empty(For("Marco Bellini"));
    }

    [Fact]
    public void Leilani_has_only_a_title_mismatch()
    {
        // Her second number isn't in the master list, but her first is; her "Guest of" position is skipped.
        var title = Assert.Single(For("Leilani Fonua"));
        Assert.Equal(DiscrepancyType.TitleMismatch, title.Type);
        Assert.Equal("Marco Bellini", title.HostDisplayName);

        // "Please specify your title" (Dr) wins over Title (Other).
        Assert.Equal("Dr", title.SourceValue);
        Assert.Equal("Ms", title.ProcessedValue);
    }

    [Fact]
    public void William_has_no_number_in_common_with_his_two_line_phone_cell()
    {
        var mobile = For("William Hartley").Single(d => d.Type == DiscrepancyType.MobileNumberMismatch);
        Assert.Equal("Nothing in common", mobile.Detail);
        Assert.Equal("0491574632", mobile.SourceValue);
        Assert.Equal("0491576398\n0491576801", mobile.ProcessedValue);
        Assert.Equal([17, 18], mobile.SourceRows.Select(r => r.RowNumber));
        Assert.All(mobile.SourceRows, r => Assert.Equal(SampleWorkbookBuilder.SuiteSheet, r.SheetName));
        Assert.Equal(new RowReference(SampleWorkbookBuilder.ProcessedSheetName, 14), Assert.Single(mobile.ProcessedRows));
    }

    [Fact]
    public void One_matching_value_is_enough_for_phones_and_emails()
    {
        Assert.Empty(For("Alice Romano"));  // two numbers in her Phone cell, one matches
        Assert.Empty(For("Luca Di Marco")); // his export email is in Email 2, not Email 1
    }

    [Fact]
    public void Alice_has_no_discrepancies() => Assert.Empty(For("Alice Romano"));

    [Fact]
    public void Olivia_matches_by_name_with_an_email_and_a_post_nominal_mismatch()
    {
        Assert.Equal([DiscrepancyType.EmailMismatch, DiscrepancyType.PostNominalMismatch], TypesFor("Olivia Grant-Taylor"));

        var email = For("Olivia Grant-Taylor").Single(d => d.Type == DiscrepancyType.EmailMismatch);
        Assert.Equal(MatchMethod.Name, email.MatchedBy);
        Assert.Equal("Olivia Grant Taylor", email.SourceDisplayName);

        var postNominal = For("Olivia Grant-Taylor").Single(d => d.Type == DiscrepancyType.PostNominalMismatch);
        Assert.Equal("OAM", postNominal.SourceValue);
        Assert.Equal("AM", postNominal.ProcessedValue);
    }

    [Fact]
    public void Ethan_has_email_missing_in_source_and_his_grid_gala_1_matches_having_no_ticket()
    {
        var email = Assert.Single(For("Ethan Cole"));
        Assert.Equal(DiscrepancyType.EmailMismatch, email.Type);
        Assert.Equal("Missing in Source", email.Detail);
    }

    [Fact]
    public void Clara_is_attending_but_still_marked_1_in_the_master_list()
    {
        var dates = Assert.Single(For("Clara Walsh"));
        Assert.Equal(DiscrepancyType.DatesMismatch, dates.Type);
        var gala = Assert.Single(dates.Days, d => d.IsDifferent);
        Assert.Equal(EventDay.GridGala, gala.Day);
        Assert.Equal(AttendanceState.Attending, gala.Source);
        Assert.Equal(AttendanceState.AwaitingResponse, gala.Processed);
    }

    [Fact]
    public void Henry_has_a_position_mismatch_and_oscars_nil_position_is_left_alone()
    {
        var position = Assert.Single(For("Henry Walsh"));
        Assert.Equal(DiscrepancyType.PositionMismatch, position.Type);
        Assert.Equal("Managing Director", position.SourceValue);
        Assert.Equal("Director", position.ProcessedValue);

        Assert.Empty(For("Oscar Reid"));
    }

    [Fact]
    public void Luca_and_his_unnamed_guest_both_match_without_discrepancies()
    {
        Assert.Empty(For("Luca Di Marco"));
        Assert.Empty(For("Unnamed guest of Luca Di Marco"));
    }

    [Fact]
    public void A_master_list_guest_with_no_ticket_in_the_export_is_not_reported()
    {
        // Ethan's Guest Name says "guest" and the export has no ticket for that guest.
        Assert.Empty(For("Unnamed guest of Ethan Cole"));
        Assert.DoesNotContain(Result.Discrepancies, d => d.Type == DiscrepancyType.GuestNotFound && d.HostDisplayName == "Ethan Cole");
    }

    [Fact]
    public void Master_list_guest_whose_name_is_only_in_the_export()
    {
        // "guest" in the master list; the export names the guest. The unnamed entry isn't reported, but the
        // named guest still is, so the master list can be updated with the name.
        var handler = TestHandlers.CreateCompareFilesHandler();
        using var source = new WorkbookBuilder().AddSourceSheet("RSVP",
            new SourceTicket("Saturday attending", "Eddie", "Hughes", "O1"),
            new SourceTicket("Saturday attending", "Jane", "Hughes", "O1") { BuyerFirstName = "Eddie", BuyerLastName = "Hughes" }).ToStream();
        using var processed = new WorkbookBuilder().AddProcessedSheet("Sheet1",
            new ProcessedGuest("Eddie", "Hughes") { Sat = "Y", GuestName = "guest" }).ToStream();

        var result = handler.Handle(new CompareFilesRequest(source, "s", processed, "p"));

        var notFound = Assert.Single(result.Discrepancies);
        Assert.Equal(DiscrepancyType.GuestNotFound, notFound.Type);
        Assert.Equal("Jane Hughes", notFound.GuestDisplayName);
        Assert.Equal("Only in Source", notFound.Detail);
    }

    [Fact]
    public void Sam_is_only_in_source()
    {
        var discrepancy = Assert.Single(For("Sam Fletcher"));
        Assert.Equal(DiscrepancyType.GuestNotFound, discrepancy.Type);
        Assert.Equal("Only in Source", discrepancy.Detail);
    }

    [Fact]
    public void Maeve_is_only_in_processed()
    {
        var discrepancy = Assert.Single(For("Maeve O'Connell"));
        Assert.Equal(DiscrepancyType.GuestNotFound, discrepancy.Type);
        Assert.Equal("Only in Processed", discrepancy.Detail);
        Assert.Equal("Oscar Reid", discrepancy.HostDisplayName);
    }

    [Fact]
    public void Shared_and_multi_line_phones_cause_no_mobile_mismatch()
    {
        Assert.DoesNotContain(Result.Discrepancies, d => d.Type == DiscrepancyType.MobileNumberMismatch
            && d.GuestDisplayName is "Oscar Reid" or "Henry Walsh" or "Clara Walsh" or "Felix Moreau");
        Assert.Empty(For("Felix Moreau"));
    }

    [Fact]
    public void Bill_matches_William_by_email_and_saturday_differs()
    {
        Assert.Equal([DiscrepancyType.DatesMismatch, DiscrepancyType.MobileNumberMismatch], TypesFor("William Hartley"));

        var discrepancy = For("William Hartley").Single(d => d.Type == DiscrepancyType.DatesMismatch);
        Assert.Equal(MatchMethod.Email, discrepancy.MatchedBy);
        Assert.Equal("Bill Hartley", discrepancy.SourceDisplayName);
        Assert.Equal([EventDay.Saturday], discrepancy.AffectedDays);
    }

    [Fact]
    public void Similar_names_are_hinted_but_not_matched()
    {
        Assert.Equal("Ruby McIntyre", Assert.Single(For("Ruby Macintyre")).PossibleMatch);
        Assert.Equal("Ruby Macintyre", Assert.Single(For("Ruby McIntyre")).PossibleMatch);
    }

    [Fact]
    public void Unknown_ticket_type_is_the_only_warning()
    {
        var warning = Assert.Single(Result.Warnings);
        Assert.Equal("Source", warning.File);
        Assert.Contains("Sunday waitlist", warning.Message);
    }

    [Fact]
    public void Totals_and_ordering()
    {
        Assert.Equal(12, Result.Discrepancies.Count);
        Assert.Equal(14, Result.SourceGuestCount);
        Assert.Equal(15, Result.ProcessedGuestCount); // including Ethan's and Luca's unnamed guests
        Assert.Equal(12, Result.MatchedCount);

        // Ordered by name, with each guest grouped under their host (Clara after Henry, Leilani where Marco
        // would be, Maeve where Oscar Reid would be), then by type.
        Assert.Equal(
            [
                "Ethan Cole", "Henry Walsh", "Clara Walsh", "Leilani Fonua", "Olivia Grant-Taylor", "Olivia Grant-Taylor",
                "Maeve O'Connell", "Ruby Macintyre", "Ruby McIntyre", "Sam Fletcher", "William Hartley", "William Hartley",
            ],
            Result.Discrepancies.Select(d => d.GuestDisplayName));
    }
}
