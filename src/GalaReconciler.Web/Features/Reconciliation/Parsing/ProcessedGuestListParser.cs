using System.Globalization;
using System.Text.RegularExpressions;
using GalaReconciler.Web.Features.Reconciliation.Domain;
using GalaReconciler.Web.Features.Reconciliation.Normalization;

namespace GalaReconciler.Web.Features.Reconciliation.Parsing;

/// <summary>
/// Reads the hand-maintained master guest list (first sheet, one row per person). Columns are
/// found by header text because the sheet has hidden columns and a blank header in column A.
/// </summary>
public sealed partial class ProcessedGuestListParser
{
    public const string FileLabel = "Processed file (master guest list)";
    private const string WarningFile = "Processed";

    public static class Headers
    {
        public const string Prenom = "Prenom";
        public const string FirstName = "First Name";
        public const string Surname = "Surname";
        public const string Postnom = "Postnom";
        public const string GuestName = "Guest Name";
        public const string Position = "Position";
        public const string Organisation = "Organisation";
        public const string Phone = "Phone";
        public const string Email1 = "Email 1";
        public const string Email2 = "Email 2";
        public const string Comments = "Comments";
        public const string Dietary = "Dietary";
        public const string Accessibility = "Accessibility";
    }

    private static readonly string[] RequiredHeaders =
    [
        .. EventDays.All.Select(d => d.ProcessedHeader),
        Headers.FirstName, Headers.Surname, Headers.GuestName, Headers.Position, Headers.Phone, Headers.Email1,
    ];

    public ParseResult Parse(Stream stream)
    {
        using var workbook = WorkbookFormatException.OpenWorkbook(stream, FileLabel);
        if (workbook.Worksheets.Count == 0)
        {
            throw new WorkbookFormatException(FileLabel, $"The {FileLabel} has no sheets.");
        }

        var sheet = workbook.Worksheet(1);
        if (sheet.LastRowUsed() is null)
        {
            throw new WorkbookFormatException(FileLabel, $"The first sheet ('{sheet.Name}') of the {FileLabel} is empty.");
        }

        var map = new HeaderMap(sheet, StringComparison.OrdinalIgnoreCase);
        var missing = map.Missing(RequiredHeaders);
        if (missing.Count > 0)
        {
            throw new WorkbookFormatException(
                FileLabel,
                $"Sheet '{sheet.Name}' in the {FileLabel} is missing required column(s): {string.Join(", ", missing)}. Headers must be in row 1.",
                missing);
        }

        var dayColumns = EventDays.All.Select(info => (Info: info, Column: map.Find(info.ProcessedHeader)!.Value)).ToList();
        var firstNameColumn = map.Find(Headers.FirstName);
        var surnameColumn = map.Find(Headers.Surname);
        var prenomColumn = map.Find(Headers.Prenom);
        var guestNameColumn = map.Find(Headers.GuestName);
        var positionColumn = map.Find(Headers.Position);
        var phoneColumn = map.Find(Headers.Phone);
        var email1Column = map.Find(Headers.Email1);
        var email2Column = map.Find(Headers.Email2);

        var warnings = new List<ReconciliationWarning>();
        var records = new List<GuestRecord>();
        var byKey = new Dictionary<string, GuestRecord>(StringComparer.Ordinal);
        var guestLists = new Dictionary<string, GuestList>(StringComparer.Ordinal);

        var lastRow = sheet.LastRowUsed()!.RowNumber();
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            string Cell(int? column) => CellText.Read(row, column);

            var firstName = Cell(firstNameColumn);
            var surname = Cell(surnameColumn);
            var matchKey = NameNormalizer.Key(firstName, surname);
            if (matchKey.Length == 0)
            {
                var hasDetails = dayColumns.Any(d => Cell(d.Column).Length > 0)
                    || Cell(phoneColumn).Length > 0 || Cell(email1Column).Length > 0 || Cell(guestNameColumn).Length > 0;
                if (hasDetails)
                {
                    warnings.Add(new(WarningFile, $"{sheet.Name} row {rowNumber} has details but no First Name or Surname, so it was skipped."));
                }

                continue;
            }

            var record = new GuestRecord
            {
                DisplayName = NameNormalizer.CleanDisplay($"{NameNormalizer.StripTitles(firstName)} {surname}"),
                MatchKey = matchKey,
                Title = NullIfEmpty(Cell(prenomColumn)),
            };
            record.Rows.Add(new RowReference(sheet.Name, rowNumber));

            foreach (var (info, column) in dayColumns)
            {
                var raw = Cell(column);
                if (!TryParseDayCell(raw, out var state))
                {
                    warnings.Add(new(WarningFile, $"{sheet.Name} row {rowNumber}: '{raw}' under {info.ProcessedHeader} for {record.DisplayName} isn't Y, N or a number. Treated as no response."));
                }

                record.Attendance[info.Day] = state;
            }

            record.Phones.UnionWith(PhoneNormalizer.NormalizeAll(Cell(phoneColumn)));
            record.Emails.UnionWith(EmailNormalizer.NormalizeAll(Cell(email1Column)));
            record.Emails.UnionWith(EmailNormalizer.NormalizeAll(Cell(email2Column)));

            var position = GuestOf().Match(Cell(positionColumn));
            if (position.Success)
            {
                record.HostDisplayName = NameNormalizer.StripTitles(position.Groups[1].Value);
                record.HostMatchKey = NameNormalizer.Normalize(position.Groups[1].Value);
            }

            if (byKey.TryGetValue(matchKey, out var existing))
            {
                warnings.Add(new(WarningFile, $"{record.DisplayName} appears more than once ({sheet.Name} rows {string.Join(", ", existing.Rows.Select(r => r.RowNumber))}, {rowNumber}). The rows were combined."));
                Merge(existing, record);
            }
            else
            {
                byKey[matchKey] = record;
                records.Add(record);
            }

            var guestEntries = SplitLines(Cell(guestNameColumn));
            if (guestEntries.Count > 0)
            {
                if (!guestLists.TryGetValue(matchKey, out var list))
                {
                    guestLists[matchKey] = list = new GuestList();
                }

                foreach (var entry in guestEntries)
                {
                    if (UnnamedGuestEntry().IsMatch(entry))
                    {
                        list.UnnamedCount++;
                    }
                    else
                    {
                        list.Named.Add(entry);
                    }
                }
            }
        }

        LinkGuests(records, byKey, guestLists, warnings);
        return new ParseResult(records, warnings);
    }

    /// <summary>Y or a positive number = attending, N = not attending, blank = no response.</summary>
    public static bool TryParseDayCell(string? value, out AttendanceState state)
    {
        var text = value?.Trim() ?? "";
        state = AttendanceState.NoResponse;

        if (text.Length == 0)
        {
            return true;
        }

        if (text.Equals("Y", StringComparison.OrdinalIgnoreCase) || text.Equals("Yes", StringComparison.OrdinalIgnoreCase))
        {
            state = AttendanceState.Attending;
            return true;
        }

        if (text.Equals("N", StringComparison.OrdinalIgnoreCase) || text.Equals("No", StringComparison.OrdinalIgnoreCase))
        {
            state = AttendanceState.NotAttending;
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            state = number > 0 ? AttendanceState.Attending : AttendanceState.NotAttending;
            return true;
        }

        return false;
    }

    private static void LinkGuests(
        List<GuestRecord> records,
        Dictionary<string, GuestRecord> byKey,
        Dictionary<string, GuestList> guestLists,
        List<ReconciliationWarning> warnings)
    {
        foreach (var (hostKey, guestList) in guestLists)
        {
            var host = byKey[hostKey];

            // "guest" with no name: give it the host's days so it can match the Source's extra ticket.
            for (var index = 1; index <= guestList.UnnamedCount; index++)
            {
                var guest = new GuestRecord
                {
                    DisplayName = UnnamedGuest.DisplayName(host.DisplayName, index),
                    MatchKey = UnnamedGuest.MatchKey(hostKey, index),
                    HostDisplayName = host.DisplayName,
                    HostMatchKey = hostKey,
                    IsUnnamedGuest = true,
                };
                foreach (var (day, state) in host.Attendance)
                {
                    guest.Attendance[day] = state;
                }

                guest.Rows.AddRange(host.Rows);
                if (byKey.TryAdd(guest.MatchKey, guest))
                {
                    records.Add(guest);
                }
            }

            // Named guests: cross-check against each guest's own "Guest of …" Position.
            foreach (var name in guestList.Named)
            {
                if (!byKey.TryGetValue(NameNormalizer.Normalize(name), out var guest))
                {
                    warnings.Add(new(WarningFile, $"{host.DisplayName} lists '{name}' under Guest Name, but there is no row for them."));
                    continue;
                }

                if (guest.HostMatchKey is null)
                {
                    guest.HostMatchKey = hostKey;
                }
                else if (guest.HostMatchKey != hostKey)
                {
                    warnings.Add(new(WarningFile, $"{guest.DisplayName}'s Position says Guest of {guest.HostDisplayName}, but {host.DisplayName} lists them under Guest Name."));
                }
            }
        }

        foreach (var record in records.Where(r => r.HostMatchKey is not null))
        {
            if (byKey.TryGetValue(record.HostMatchKey!, out var host))
            {
                record.HostDisplayName = host.DisplayName;
            }
            else
            {
                warnings.Add(new(WarningFile, $"{record.DisplayName} is marked as Guest of {record.HostDisplayName}, but there is no row for {record.HostDisplayName}."));
            }
        }
    }

    private static void Merge(GuestRecord target, GuestRecord duplicate)
    {
        target.Rows.AddRange(duplicate.Rows);
        target.Phones.UnionWith(duplicate.Phones);
        target.Emails.UnionWith(duplicate.Emails);
        target.Title ??= duplicate.Title;
        if (target.HostMatchKey is null)
        {
            target.HostMatchKey = duplicate.HostMatchKey;
            target.HostDisplayName = duplicate.HostDisplayName;
        }

        foreach (var (day, state) in duplicate.Attendance)
        {
            // Attending beats not attending beats no response (enum order).
            if (state < target.GetAttendance(day))
            {
                target.Attendance[day] = state;
            }
        }
    }

    private static List<string> SplitLines(string cell) =>
        cell.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NameNormalizer.CleanDisplay)
            .Where(line => line.Length > 0)
            .ToList();

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    [GeneratedRegex(@"^\s*guest\s+of\s+(.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex GuestOf();

    [GeneratedRegex(@"^guest$", RegexOptions.IgnoreCase)]
    private static partial Regex UnnamedGuestEntry();

    private sealed class GuestList
    {
        public List<string> Named { get; } = [];

        public int UnnamedCount { get; set; }
    }
}
