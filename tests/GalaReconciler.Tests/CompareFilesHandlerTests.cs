using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;
using GalaReconciler.Core;
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
    public void Marco_has_only_a_mobile_mismatch()
    {
        var discrepancy = Assert.Single(For("Marco Bellini"));
        Assert.Equal(DiscrepancyType.MobileNumberMismatch, discrepancy.Type);
        Assert.Equal("Only in Source: 049157156", discrepancy.Detail);
        Assert.Equal([2, 4, 6, 8], discrepancy.SourceRows.Select(r => r.RowNumber));
        Assert.All(discrepancy.SourceRows, r => Assert.Equal(SampleWorkbookBuilder.SuiteSheet, r.SheetName));
        Assert.Equal(new RowReference(SampleWorkbookBuilder.ProcessedSheetName, 2), Assert.Single(discrepancy.ProcessedRows));
    }

    [Fact]
    public void Leilani_has_only_a_mobile_mismatch_and_is_marcos_guest()
    {
        var discrepancy = Assert.Single(For("Leilani Fonua"));
        Assert.Equal(DiscrepancyType.MobileNumberMismatch, discrepancy.Type);
        Assert.Equal("Only in Source: 0491570158", discrepancy.Detail);
        Assert.Equal("Marco Bellini", discrepancy.HostDisplayName);
    }

    [Fact]
    public void Alice_has_no_discrepancies() => Assert.Empty(For("Alice Romano"));

    [Fact]
    public void Olivia_matches_by_name_with_only_an_email_mismatch()
    {
        var discrepancy = Assert.Single(For("Olivia Grant-Taylor"));
        Assert.Equal(DiscrepancyType.EmailMismatch, discrepancy.Type);
        Assert.Equal(MatchMethod.Name, discrepancy.MatchedBy);
        Assert.Equal("Olivia Grant Taylor", discrepancy.SourceDisplayName);
    }

    [Fact]
    public void Ethan_has_email_missing_in_source_and_a_grid_gala_dates_mismatch()
    {
        Assert.Equal([DiscrepancyType.DatesMismatch, DiscrepancyType.EmailMismatch], TypesFor("Ethan Cole"));

        var email = For("Ethan Cole").Single(d => d.Type == DiscrepancyType.EmailMismatch);
        Assert.Equal("Missing in Source", email.Detail);

        var dates = For("Ethan Cole").Single(d => d.Type == DiscrepancyType.DatesMismatch);
        Assert.Equal([EventDay.GridGala], dates.AffectedDays);
    }

    [Fact]
    public void Luca_and_his_unnamed_guest_both_match_without_discrepancies()
    {
        Assert.Empty(For("Luca Di Marco"));
        Assert.Empty(For("Unnamed guest of Luca Di Marco"));
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
    public void Shared_phone_and_multi_line_phone_guests_have_no_discrepancies()
    {
        Assert.Empty(For("Oscar Reid"));
        Assert.Empty(For("Henry Walsh"));
        Assert.Empty(For("Clara Walsh"));
        Assert.Empty(For("Felix Moreau"));
    }

    [Fact]
    public void Bill_matches_William_by_email_and_saturday_differs()
    {
        var discrepancy = Assert.Single(For("William Hartley"));
        Assert.Equal(DiscrepancyType.DatesMismatch, discrepancy.Type);
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
        Assert.Equal(10, Result.Discrepancies.Count);
        Assert.Equal(14, Result.SourceGuestCount);
        Assert.Equal(14, Result.ProcessedGuestCount);
        Assert.Equal(12, Result.MatchedCount);

        // Ordered by name, with each guest grouped under their host: Leilani comes straight after Marco,
        // and Maeve sorts where her host Oscar Reid would.
        Assert.Equal(
            [
                "Ethan Cole", "Ethan Cole", "Marco Bellini", "Leilani Fonua", "Olivia Grant-Taylor",
                "Maeve O'Connell", "Ruby Macintyre", "Ruby McIntyre", "Sam Fletcher", "William Hartley",
            ],
            Result.Discrepancies.Select(d => d.GuestDisplayName));
    }
}
