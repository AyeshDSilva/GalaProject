namespace GalaReconciler.Core.Domain;

/// <summary>Where a record came from in the original workbook, so the user can find it.</summary>
public sealed record RowReference(string SheetName, int RowNumber, string? OrderId = null)
{
    /// <summary>"Thurs_Sun Suite RSVP · rows 2, 4, 6, 8 · Order Q7RT2MXA", one line per sheet.</summary>
    public static IReadOnlyList<string> Describe(IEnumerable<RowReference> rows) =>
        rows.GroupBy(r => r.SheetName)
            .Select(sheet =>
            {
                var numbers = sheet.Select(r => r.RowNumber).Distinct().Order().ToList();
                var orders = sheet.Select(r => r.OrderId).OfType<string>().Distinct().ToList();
                var text = $"{sheet.Key} · {(numbers.Count == 1 ? "row" : "rows")} {string.Join(", ", numbers)}";
                return orders.Count == 0 ? text : $"{text} · {(orders.Count == 1 ? "Order" : "Orders")} {string.Join(", ", orders)}";
            })
            .ToList();
}

/// <summary>One person (or unnamed guest) as read from either file.</summary>
public sealed class GuestRecord
{
    public required string DisplayName { get; init; }

    public string? Title { get; set; }

    public string? PostNominal { get; set; }

    /// <summary>Position title. In the master list this can also be a host link ("Guest of Mr …").</summary>
    public string? Position { get; set; }

    /// <summary>Normalized "first last", see <c>NameNormalizer</c>.</summary>
    public required string MatchKey { get; init; }

    /// <summary>
    /// Other normalized names the export has for this person: the ticket holder name in columns B/C when it
    /// differs from the attendee name, and a different spelling in the other export. Tried after exact names.
    /// </summary>
    public HashSet<string> OtherMatchKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>Who this person is a guest of, if anyone.</summary>
    public string? HostDisplayName { get; set; }

    public string? HostMatchKey { get; set; }

    public bool IsUnnamedGuest { get; init; }

    public Dictionary<EventDay, AttendanceState> Attendance { get; } = [];

    public HashSet<string> Phones { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Emails { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Dietary requirements. From the RSVP exports: every distinct answer across the person's tickets and all their
    /// dietary columns, split into parts. From the master list: the Dietary cell as written.
    /// </summary>
    public List<string> Dietary { get; } = [];

    /// <summary>Accessibility requirements, read the same way as <see cref="Dietary"/>.</summary>
    public List<string> Accessibility { get; } = [];

    public List<RowReference> Rows { get; } = [];

    public AttendanceState GetAttendance(EventDay day) =>
        Attendance.GetValueOrDefault(day, AttendanceState.NoResponse);

    /// <summary>"Thu: Attending" lines for every day with a response.</summary>
    public string DescribeAttendance()
    {
        var lines = EventDays.All
            .Where(info => GetAttendance(info.Day) != AttendanceState.NoResponse)
            .Select(info => $"{info.ShortLabel}: {GetAttendance(info.Day).ToDisplay()}")
            .ToList();
        return lines.Count == 0 ? "No days recorded" : string.Join('\n', lines);
    }
}
