namespace GalaReconciler.Tests.Helpers;

/// <summary>
/// Realistic sample files for manual testing (written to samples/ by <c>SampleWorkbookTests</c>) and for the
/// end-to-end handler test. Each person exercises a specific behaviour, noted alongside.
/// </summary>
public static class SampleWorkbookBuilder
{
    public const string SuiteSheet = "Thurs_Sun Suite RSVP";
    public const string GalaSheet = "Grid Gala RSVP";
    public const string ProcessedSheetName = "Sheet1";

    public static WorkbookBuilder Source() => new WorkbookBuilder()
        .AddSourceSheet(
            SuiteSheet,
            // Marco (rows 2, 4, 6, 8) and his guest Leilani (rows 3, 5, 7, 9) on one order, interleaved.
            // Marco's Friday number is a numeric cell (leading zero lost); his Sunday number has a typo.
            Marco("Thursday attending", "0491570156"),
            Leilani("Thursday attending", "0491570157"),
            Marco("Friday attending", 491570156),
            Leilani("Friday attending", "0491 570 157"),
            Marco("Saturday attending", "0491570156"),
            Leilani("Saturday attending", "0491570158"),
            Marco("Sunday attending", "049157156"),
            Leilani("Sunday attending", "0491570157"),
            // Row 10: identical in both files ("Ms." and "Ms" count as the same title).
            new SourceTicket("Saturday attending", "Alice", "Romano", "K3LMP9QA")
            {
                Title = "Ms.", Mobile = "0491570159", Email = "alice.romano@example.com",
            },
            // Row 11: "Grant Taylor" vs "Grant-Taylor"; different email; post nominal from "Please specify" (OAM) vs AM.
            new SourceTicket("Saturday attending", "Olivia", "Grant Taylor", "Q8WTR2ZN")
            {
                Title = "Ms", PostNominal = "Other", PostNominalOther = "OAM", Mobile = "0491570110", Email = "olivia.grant@example.com",
            },
            // Row 12: bought by an assistant; buyer contact details must not be used.
            new SourceTicket("Saturday not attending", "Ethan", "Cole", "MH4421XP")
            {
                Title = "Mr", BuyerFirstName = "Nina", BuyerLastName = "Patel",
                BuyerMobile = "0491 575 789", BuyerEmail = "nina.patel@example.net",
            },
            // Rows 13-14: two Sunday tickets on one order under one name = Luca + an unnamed guest.
            new SourceTicket("Sunday not attending", "Luca", "Di Marco", "ZK4W8NPL")
            {
                Title = "Mr", Telephone = "0491 574 118", Email = "luca.dimarco@example.com",
            },
            new SourceTicket("Sunday not attending", "Luca", "Di Marco", "ZK4W8NPL")
            {
                Title = "Mr", Telephone = "0491 574 118", Email = "luca.dimarco@example.com",
            },
            // Rows 15-16: not in Processed; the second ticket type is unrecognised (warning).
            new SourceTicket("Friday not attending", "Sam", "Fletcher", "CS19PLMQ")
            {
                Mobile = "0491 572 549", Email = "sam.fletcher@example.com",
            },
            new SourceTicket("Sunday waitlist", "Sam", "Fletcher", "CS19PLMQ")
            {
                Mobile = "0491 572 549", Email = "sam.fletcher@example.com",
            },
            // Rows 17-18: "Bill" here, "William" in Processed; matched by email. Saturday differs.
            new SourceTicket("Friday attending", "Bill", "Hartley", "BH77KWER")
            {
                Mobile = "0491 574 632", Email = "bill.hartley@example.com",
            },
            new SourceTicket("Saturday attending", "Bill", "Hartley", "BH77KWER")
            {
                Mobile = "0491 574 632", Email = "bill.hartley@example.com",
            },
            // Row 19: "Macintyre" vs "McIntyre" in Processed; not matched, shown as a possible match.
            new SourceTicket("Thursday attending", "Ruby", "Macintyre", "DM55TYUI")
            {
                Mobile = "0491 575 254", Email = "ruby.macintyre@example.com",
            })
        .AddSourceSheet(
            GalaSheet,
            // Mobile and landline in separate columns; Processed has both in one multi-line cell. Position "nil" is ignored.
            new SourceTicket("Grid Gala attending", "Oscar", "Reid", "GG10JW01")
            {
                Title = "Mr", Mobile = "0491570737", Telephone = "(08) 7010 0175", Email = "oscar.reid@example.com", PositionTitle = "nil",
            },
            // Three people sharing one phone number; Clara and Felix are Henry's guests. Henry's position title differs.
            new SourceTicket("Grid Gala attending", "Henry", "Walsh", "GG20TS02")
            {
                Title = "Mr", Telephone = "0491 571 266", Email = "henry.walsh@example.com", PositionTitle = "Managing Director",
            },
            new SourceTicket("Grid Gala attending", "Clara", "Walsh", "GG20TS02")
            {
                Title = "Mrs", Telephone = "0491571266", BuyerFirstName = "Henry", BuyerLastName = "Walsh",
            },
            new SourceTicket("Grid Gala attending", "Felix", "Moreau", "GG20TS02")
            {
                Title = "Mr", Telephone = "0491 571 266", BuyerFirstName = "Henry", BuyerLastName = "Walsh",
            });

    public static WorkbookBuilder Processed() => new WorkbookBuilder()
        .AddProcessedSheet(
            ProcessedSheetName,
            new ProcessedGuest("Marco", "Bellini") // row 2
            {
                Prenom = "Mr", Thurs = "Y", Fri = "Y", Sat = "Y", Sun = "Y", GuestName = "Ms Leilani Fonua",
                Position = "Managing Director", Organisation = "Bellini Group", Phone = "0491 570 156",
                Email1 = "marco.bellini@example.com",
            },
            new ProcessedGuest("Leilani", "Fonua") // row 3
            {
                Prenom = "Ms", Thurs = "Y", Fri = "Y", Sat = "Y", Sun = "Y", Position = "Guest of Mr Marco Bellini",
                Phone = "0491 570 157", Email1 = "leilani.fonua@example.com",
            },
            new ProcessedGuest("Alice", "Romano") // row 4
            {
                Prenom = "Ms", Sat = "Y", Phone = "0491 570 159", Email1 = "alice.romano@example.com", Dietary = "Vegetarian",
            },
            new ProcessedGuest("Olivia", "Grant-Taylor") // row 5: phone is a numeric cell; post nominal AM vs OAM in the export
            {
                Prenom = "Ms", Postnom = "AM", Sat = "Y", Organisation = "Harbour City FC", Phone = 491570110, Email1 = "ogrant@example.org",
            },
            null, // row 6: blank
            new ProcessedGuest("Ethan", "Cole") // row 7: Grid Gala 1 = invited, no response; he has no Gala ticket, so that matches
            {
                Prenom = "Mr", Sat = "N", GridGala = 1, Organisation = "Northwind Capital", Email1 = "ethan.cole@example.net",
            },
            new ProcessedGuest("Luca", "Di Marco") // row 8: unnamed guest
            {
                Prenom = "Mr", Sun = "N", GuestName = "guest", Phone = "0491574118", Email1 = "luca.dimarco@example.com",
            },
            new ProcessedGuest("Oscar", "Reid") // row 9: curly apostrophe in Guest Name; position kept (export says "nil")
            {
                Prenom = "Mr", GridGala = "Y", GuestName = "Ms Maeve O’Connell", Position = "Chief Executive",
                Phone = "- 0491570737\n08 7010 0175", Email1 = "oscar.reid@example.com",
            },
            new ProcessedGuest("Maeve", "O'Connell") // row 10: doubled title in Position; not in Source
            {
                Prenom = "Ms", GridGala = "Y", Position = "Guest of Mr Mr Oscar Reid", Email1 = "maeve.oconnell@example.org",
                Accessibility = "Wheelchair access",
            },
            new ProcessedGuest("Henry", "Walsh") // row 11: multi-line Guest Name; position "Director" vs "Managing Director"
            {
                Prenom = "Mr", GridGala = "Y", GuestName = "Mrs Clara Walsh\nMr Felix Moreau", Position = "Director", Phone = "0491 571 266",
                Email1 = "henry.walsh@example.com",
            },
            new ProcessedGuest("Clara", "Walsh") // row 12: Grid Gala 1 (no response yet), but she is attending in the export
            {
                Prenom = "Mrs", GridGala = 1, Position = "Guest of Mr Henry Walsh", Phone = "0491 571 266",
            },
            new ProcessedGuest("Felix", "Moreau") // row 13: "nil" email placeholder
            {
                Prenom = "Mr", GridGala = "Y", Position = "Guest of Mr Henry Walsh", Phone = "0491 571 266", Email1 = "nil",
            },
            new ProcessedGuest("William", "Hartley") // row 14
            {
                Prenom = "Mr", Fri = "Y", Sat = "N", Phone = "0491 574 632", Email1 = "bill.hartley@example.com",
            },
            new ProcessedGuest("Ruby", "McIntyre") // row 15
            {
                Prenom = "Mr", Thurs = "Y", Phone = "0491 575 254", Email1 = "ruby.mcintyre@example.org",
            });

    /// <summary>Writes Source.xlsx and Processed.xlsx into <paramref name="directory"/>.</summary>
    public static void WriteSamples(string directory)
    {
        Directory.CreateDirectory(directory);
        Source().SaveAs(Path.Combine(directory, "Source.xlsx"));
        Processed().SaveAs(Path.Combine(directory, "Processed.xlsx"));
    }

    private static SourceTicket Marco(string ticketType, object telephone) =>
        new(ticketType, "Marco", "Bellini", "Q7RT2MXA")
        {
            Title = "Mr", PositionTitle = "Managing Director", Telephone = telephone, Email = "marco.bellini@example.com",
        };

    private static SourceTicket Leilani(string ticketType, object telephone) =>
        new(ticketType, "Leilani", "Fonua", "Q7RT2MXA")
        {
            // "Please specify your title" (Dr) wins over Title. Her position title isn't compared: her row says "Guest of".
            Title = "Other", TitleOther = "Dr", PositionTitle = "Teacher", Telephone = telephone, Email = "leilani.fonua@example.com",
            BuyerFirstName = "Marco", BuyerLastName = "Bellini",
        };
}
