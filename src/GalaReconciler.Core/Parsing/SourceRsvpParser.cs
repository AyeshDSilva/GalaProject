using ClosedXML.Excel;
using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Normalization;

namespace GalaReconciler.Core.Parsing;

/// <summary>
/// Reads the ticketing system's RSVP export: every sheet, one row per ticket per day,
/// grouped into one <see cref="GuestRecord"/> per person.
/// </summary>
public sealed class SourceRsvpParser
{
    public const string FileLabel = "Source file (RSVP export)";
    private const string WarningFile = "Source";

    /// <summary>
    /// Header text, matched exactly and case-sensitively: "First name" (ticket holder, column B) and
    /// "First Name" (attendee section, after "Title") are different columns.
    /// </summary>
    public static class Headers
    {
        public const string TicketType = "Ticket type";
        public const string OrderId = "Order id";
        public const string FirstName = "First name";
        public const string LastName = "Last name";
        public const string BuyerFirstName = "Buyer first name";
        public const string BuyerLastName = "Buyer last name";
        public const string Title = "Title";
        public const string TitleOther = "Please specify your title";
        public const string AttendeeFirstName = "First Name";
        public const string AttendeeLastName = "Last Name";
        public const string Mobile = "Mobile";
        public const string Telephone = "Telephone";
        public const string Email = "Email";
    }

    private static readonly string[] RequiredHeaders =
    [
        Headers.TicketType, Headers.OrderId, Headers.FirstName, Headers.LastName,
        Headers.BuyerFirstName, Headers.BuyerLastName, Headers.AttendeeFirstName, Headers.AttendeeLastName,
        Headers.Mobile, Headers.Telephone, Headers.Email,
    ];

    public ParseResult Parse(Stream stream)
    {
        using var workbook = WorkbookFormatException.OpenWorkbook(stream, FileLabel);
        var warnings = new List<ReconciliationWarning>();
        var tickets = new List<Ticket>();
        var sheetsRead = 0;
        (string Sheet, IReadOnlyList<string> Missing)? firstSkipped = null;

        foreach (var sheet in workbook.Worksheets)
        {
            if (sheet.LastRowUsed() is null)
            {
                continue;
            }

            var map = new HeaderMap(sheet, StringComparison.Ordinal);
            var missing = map.Missing(RequiredHeaders);

            if (!map.Contains(Headers.TicketType) && !map.Contains(Headers.OrderId))
            {
                // Not an RSVP sheet at all (e.g. a summary tab): skip it rather than fail the whole file.
                firstSkipped ??= (sheet.Name, missing);
                warnings.Add(new(WarningFile, $"Sheet '{sheet.Name}' was skipped because it has no 'Ticket type' or 'Order id' column."));
                continue;
            }

            if (missing.Count > 0)
            {
                throw MissingHeaders(sheet.Name, missing);
            }

            ReadSheet(sheet, ResolveColumns(map), tickets, warnings);
            sheetsRead++;
        }

        if (sheetsRead == 0)
        {
            throw firstSkipped is { } skipped
                ? MissingHeaders(skipped.Sheet, skipped.Missing)
                : new WorkbookFormatException(FileLabel, $"The {FileLabel} is empty.");
        }

        return new ParseResult(BuildRecords(tickets, warnings), warnings);
    }

    private static WorkbookFormatException MissingHeaders(string sheetName, IReadOnlyList<string> missing) =>
        new(
            FileLabel,
            $"Sheet '{sheetName}' in the {FileLabel} is missing required column(s): {string.Join(", ", missing)}. " +
            "Headers must be in row 1 and match exactly, including capital letters.",
            missing);

    private static Columns ResolveColumns(HeaderMap map)
    {
        // Attendee columns come after "Title"; searching from there keeps them apart from the
        // ticket-holder and buyer columns even if a header is ever repeated.
        var title = map.Find(Headers.Title);
        var attendeeStart = title ?? 0;
        int Find(string header, int after = 0) => map.Find(header, after) ?? map.Find(header)!.Value;

        return new Columns(
            TicketType: Find(Headers.TicketType),
            OrderId: Find(Headers.OrderId),
            FirstName: Find(Headers.FirstName),
            LastName: Find(Headers.LastName),
            BuyerFirstName: Find(Headers.BuyerFirstName),
            BuyerLastName: Find(Headers.BuyerLastName),
            Title: title,
            TitleOther: map.Find(Headers.TitleOther),
            AttendeeFirstName: Find(Headers.AttendeeFirstName, attendeeStart),
            AttendeeLastName: Find(Headers.AttendeeLastName, attendeeStart),
            Mobile: Find(Headers.Mobile, attendeeStart),
            Telephone: Find(Headers.Telephone, attendeeStart),
            Email: Find(Headers.Email, attendeeStart));
    }

    private static void ReadSheet(IXLWorksheet sheet, Columns columns, List<Ticket> tickets, List<ReconciliationWarning> warnings)
    {
        var lastRow = sheet.LastRowUsed()!.RowNumber();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            string Cell(int? column) => CellText.Read(row, column);

            var ticketType = Cell(columns.TicketType);
            var orderId = Cell(columns.OrderId);

            var firstName = Cell(columns.AttendeeFirstName);
            var lastName = Cell(columns.AttendeeLastName);
            if (firstName.Length == 0 && lastName.Length == 0)
            {
                firstName = Cell(columns.FirstName);
                lastName = Cell(columns.LastName);
            }

            var matchKey = NameNormalizer.Key(firstName, lastName);
            if (matchKey.Length == 0)
            {
                if (ticketType.Length > 0 || orderId.Length > 0)
                {
                    warnings.Add(new(WarningFile, $"{sheet.Name} row {rowNumber}: ticket has no attendee name and was skipped."));
                }

                continue;
            }

            var displayName = NameNormalizer.CleanDisplay($"{NameNormalizer.StripTitles(firstName)} {lastName}");

            EventDay? day = null;
            var state = AttendanceState.NoResponse;
            if (TicketTypeParser.TryParse(ticketType, out var parsedDay, out var parsedState))
            {
                day = parsedDay;
                state = parsedState;
            }
            else
            {
                var shown = ticketType.Length == 0 ? "(blank)" : $"'{ticketType}'";
                warnings.Add(new(WarningFile, $"{sheet.Name} row {rowNumber}: unrecognised ticket type {shown} for {displayName}. The row wasn't counted towards any day."));
            }

            var title = Cell(columns.Title);
            var titleOther = Cell(columns.TitleOther);
            if ((title.Length == 0 || title.Equals("Other", StringComparison.OrdinalIgnoreCase)) && titleOther.Length > 0)
            {
                title = titleOther;
            }

            // Attendee contact details only. Buyer mobile/email are deliberately ignored: the buyer is
            // often someone else (e.g. an assistant buying for an executive).
            var phones = PhoneNormalizer.NormalizeAll(Cell(columns.Mobile))
                .Concat(PhoneNormalizer.NormalizeAll(Cell(columns.Telephone)))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var emails = EmailNormalizer.NormalizeAll(Cell(columns.Email));

            var buyerFirst = Cell(columns.BuyerFirstName);
            var buyerLast = Cell(columns.BuyerLastName);

            tickets.Add(new Ticket(
                sheet.Name,
                rowNumber,
                orderId,
                displayName,
                matchKey,
                title.Length == 0 ? null : title,
                day,
                state,
                phones,
                emails,
                NameNormalizer.Key(buyerFirst, buyerLast)));
        }
    }

    private static List<GuestRecord> BuildRecords(List<Ticket> tickets, List<ReconciliationWarning> warnings)
    {
        // A second ticket for the same day, on the same order, under the same attendee name is an
        // unnamed guest of that attendee. The first such ticket stays with the attendee.
        var ownTickets = new List<Ticket>();
        var extraTickets = new Dictionary<(string HostKey, int Index), List<Ticket>>();
        var ticketsPerSlot = new Dictionary<(string OrderId, string MatchKey, EventDay Day), int>();

        foreach (var ticket in tickets)
        {
            if (ticket.Day is { } day && ticket.OrderId.Length > 0)
            {
                var slot = (ticket.OrderId, ticket.MatchKey, day);
                var seen = ticketsPerSlot.GetValueOrDefault(slot);
                ticketsPerSlot[slot] = seen + 1;
                if (seen > 0)
                {
                    var key = (ticket.MatchKey, seen);
                    if (!extraTickets.TryGetValue(key, out var list))
                    {
                        extraTickets[key] = list = [];
                    }

                    list.Add(ticket);
                    continue;
                }
            }

            ownTickets.Add(ticket);
        }

        var records = new List<GuestRecord>();
        var byKey = new Dictionary<string, GuestRecord>(StringComparer.Ordinal);
        var attendeeKeys = ownTickets.Select(t => t.MatchKey).ToHashSet(StringComparer.Ordinal);

        foreach (var group in ownTickets.GroupBy(t => t.MatchKey))
        {
            var personTickets = group.ToList();
            var record = new GuestRecord
            {
                DisplayName = personTickets[0].DisplayName,
                MatchKey = group.Key,
                Title = personTickets.Select(t => t.Title).FirstOrDefault(t => t is not null),
            };

            foreach (var ticket in personTickets)
            {
                AddContactDetails(record, ticket);
                record.Rows.Add(new RowReference(ticket.Sheet, ticket.Row, NullIfEmpty(ticket.OrderId)));
            }

            ResolveAttendance(record, personTickets, warnings);

            // A guest is someone whose tickets were all bought by another attendee. A buyer who isn't
            // attending (e.g. an assistant) isn't treated as a host.
            if (!personTickets.Any(t => t.BuyerMatchKey == group.Key))
            {
                record.HostMatchKey = personTickets
                    .Select(t => t.BuyerMatchKey)
                    .FirstOrDefault(buyer => buyer.Length > 0 && attendeeKeys.Contains(buyer));
            }

            records.Add(record);
            byKey[record.MatchKey] = record;
        }

        foreach (var record in records.Where(r => r.HostMatchKey is not null))
        {
            record.HostDisplayName = byKey[record.HostMatchKey!].DisplayName;
        }

        foreach (var ((hostKey, index), guestTickets) in extraTickets)
        {
            var host = byKey[hostKey];
            var guest = new GuestRecord
            {
                DisplayName = UnnamedGuest.DisplayName(host.DisplayName, index),
                MatchKey = UnnamedGuest.MatchKey(hostKey, index),
                HostDisplayName = host.DisplayName,
                HostMatchKey = hostKey,
                IsUnnamedGuest = true,
            };

            foreach (var ticket in guestTickets)
            {
                // The repeated ticket carries the host's own contact details, so they belong to the host.
                AddContactDetails(host, ticket);
                guest.Rows.Add(new RowReference(ticket.Sheet, ticket.Row, NullIfEmpty(ticket.OrderId)));
            }

            ResolveAttendance(guest, guestTickets, warnings);
            records.Add(guest);
        }

        return records;
    }

    private static void ResolveAttendance(GuestRecord record, List<Ticket> tickets, List<ReconciliationWarning> warnings)
    {
        foreach (var dayTickets in tickets.Where(t => t.Day is not null).GroupBy(t => t.Day!.Value))
        {
            var label = EventDays.Get(dayTickets.Key).TicketTypeLabel;
            var attending = dayTickets.Any(t => t.State == AttendanceState.Attending);
            var notAttending = dayTickets.Any(t => t.State == AttendanceState.NotAttending);

            var orders = dayTickets.Select(t => t.OrderId).Where(o => o.Length > 0).Distinct().ToList();
            if (orders.Count > 1)
            {
                warnings.Add(new(WarningFile, $"{record.DisplayName} has tickets for {label} on more than one order ({string.Join(", ", orders)}): {DescribeRows(dayTickets)}."));
            }

            if (attending && notAttending)
            {
                warnings.Add(new(WarningFile, $"{record.DisplayName} is both attending and not attending on {label} ({DescribeRows(dayTickets)}). Treated as attending."));
            }

            record.Attendance[dayTickets.Key] = attending ? AttendanceState.Attending : AttendanceState.NotAttending;
        }
    }

    private static void AddContactDetails(GuestRecord record, Ticket ticket)
    {
        record.Phones.UnionWith(ticket.Phones);
        record.Emails.UnionWith(ticket.Emails);
    }

    private static string DescribeRows(IEnumerable<Ticket> tickets) =>
        string.Join("; ", tickets.GroupBy(t => t.Sheet).Select(g => $"{g.Key} row {string.Join(", ", g.Select(t => t.Row))}"));

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private sealed record Columns(
        int TicketType,
        int OrderId,
        int FirstName,
        int LastName,
        int BuyerFirstName,
        int BuyerLastName,
        int? Title,
        int? TitleOther,
        int AttendeeFirstName,
        int AttendeeLastName,
        int Mobile,
        int Telephone,
        int Email);

    private sealed record Ticket(
        string Sheet,
        int Row,
        string OrderId,
        string DisplayName,
        string MatchKey,
        string? Title,
        EventDay? Day,
        AttendanceState State,
        IReadOnlyList<string> Phones,
        IReadOnlyList<string> Emails,
        string BuyerMatchKey);
}
