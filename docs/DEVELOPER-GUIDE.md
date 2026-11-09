# Developer guide

How to find your way around the code and make the common changes: renaming or adding columns, changing how
values are read and compared, and adding a new check. For what the app does and how to run it, see the
[README](../README.md).

- [How a comparison flows](#how-a-comparison-flows)
- [Where to change what](#where-to-change-what)
- [Columns each file must have](#columns-each-file-must-have)
- [Recipes](#recipes)
  1. [A column was renamed in the RSVP export](#1-a-column-was-renamed-in-the-rsvp-export)
  2. [A column was renamed in the master list](#2-a-column-was-renamed-in-the-master-list)
  3. [Make a column required or optional](#3-make-a-column-required-or-optional)
  4. [Change how a value is cleaned before comparing](#4-change-how-a-value-is-cleaned-before-comparing)
  5. [Change the day / attendance logic](#5-change-the-day--attendance-logic)
  6. [Compare a new field (worked example: Organisation)](#6-compare-a-new-field-worked-example-organisation)
  7. [Change how people are matched](#7-change-how-people-are-matched)
  8. [Add a warning](#8-add-a-warning)
- [Development loop](#development-loop)
- [Finding things fast](#finding-things-fast)
- [Before you ship](#before-you-ship)

## How a comparison flows

```
RSVP export (.xlsx)          Master list (.xlsx)
      │                             │
SourceRsvpParser            ProcessedGuestListParser        Core/Parsing/
      │  one GuestRecord per person │
      └──────────────┬──────────────┘
                GuestMatcher                                 Core/Matching/
                     │  MatchedPair (Source + Processed)
             IDiscrepancyRule × N                            Core/Rules/
                     │  Discrepancy list
             ReconciliationResult                            Core/CompareFilesHandler.cs
          ┌──────────┴───────────┐
  Cli: ExcelReportWriter   Web: RunStore (SQLite)
       ProcessedFileHighlighter     Results.cshtml
```

Three ideas run through the code:

1. **Columns are found by header text, never by letter** ([HeaderMap.cs](../src/GalaReconciler.Core/Parsing/HeaderMap.cs)).
   Every header the app reads is a constant in a `Headers` class at the top of each parser. Change it there and
   everything that uses it follows.
2. **Both files become the same shape.** Each parser turns its file into a list of
   [`GuestRecord`](../src/GalaReconciler.Core/Domain/GuestRecord.cs)s (name, title, days, phones, emails, host…).
   Rules only ever compare two `GuestRecord`s, so they don't know about spreadsheets.
3. **Values are cleaned before they're compared**, by the classes in `Core/Normalization/`. That's where
   "`Mr.` = `Mr`" or "`0491 570 156` = `61491570156`" lives.

## Where to change what

| I want to… | Change | Also update |
|---|---|---|
| Rename an RSVP export column | `SourceRsvpParser.Headers` | test `SourceSheet.Headers` |
| Rename a master list column | `ProcessedGuestListParser.Headers` | test `ProcessedSheet.Headers` |
| Rename a day column / ticket label | `EventDays.All` in `Domain/EventDay.cs` | test helpers (day headers) |
| Add an event day | `EventDay` enum + `EventDays.All` | see [README](../README.md#adding-a-new-event-day) |
| Make a column required / optional | `RequiredHeaders` in the parser | `WorkbookDetector` if it's used to recognise the file |
| Change what counts as blank (`nil`, `n/a`…) | `TextNormalizer.Placeholders` | `NormalizerTests.cs` |
| Change phone or email cleaning | `PhoneNormalizer` / `EmailNormalizer` | `NormalizerTests.cs` |
| Change what `Y` / `N` / `1` mean | `ProcessedGuestListParser.TryParseDayCell` | `ProcessedGuestListParserTests.cs` |
| Change which day answers count as different | `DatesMismatchRule.Differs` | `RuleTests.cs` |
| Change how a ticket type is read | `TicketTypeParser` | `TicketTypeParserTests.cs` |
| Change how people are paired up | `GuestMatcher` | `GuestMatcherTests.cs` |
| Compare a new field | see [recipe 6](#6-compare-a-new-field-worked-example-organisation) | |
| Change which cells get highlighted | `ProcessedFileHighlighter.Marks` | `CliTests.cs` |
| Change the results workbook | `ExcelReportWriter` | `CliTests.cs` |
| Change the web results page | `Features/Reconciliation/Results.cshtml` | `WebSmokeTests.cs` |

All paths are under `src/GalaReconciler.Core/` unless they name the Cli or Web project; tests are under
`tests/GalaReconciler.Tests/`.

## Columns each file must have

Headers must be in **row 1**. Extra columns anywhere are ignored. If a header appears twice, the leftmost one
is used.

**RSVP export** ([SourceRsvpParser.cs](../src/GalaReconciler.Core/Parsing/SourceRsvpParser.cs)). Every sheet
with `Ticket type` or `Order id` is read. Headers must match **exactly, including capitals**, because the export
has both `First name` (ticket holder) and `First Name` (attendee).

| Header | Required | Used for |
|---|---|---|
| `Ticket type` | yes | day + attending / not attending |
| `Order id` | yes | grouping tickets; spotting an unnamed guest's second ticket |
| `First name`, `Last name` | yes | ticket holder name, used when the attendee name is blank |
| `Buyer first name`, `Buyer last name` | yes | working out who is a guest of whom |
| `First Name`, `Last Name` | yes | attendee name (searched for after `Title`) |
| `Mobile`, `Telephone` | yes | phones (searched for after `Title`) |
| `Email` | yes | emails (searched for after `Title`) |
| `Title`, `Please specify your title` | no | title (the "specify" answer wins) |
| `Post nominal`, `Please specify your post nominal` | no | post nominal (the "specify" answer wins) |
| `Position title` | no | position |

**Master list** ([ProcessedGuestListParser.cs](../src/GalaReconciler.Core/Parsing/ProcessedGuestListParser.cs)).
Only the **first sheet** is read. Headers are matched **ignoring capitals**.

| Header | Required | Used for |
|---|---|---|
| `THURS`, `FRI`, `SAT`, `SUN`, `Grid Gala` | yes | days (from `EventDays.All`) |
| `First Name`, `Surname` | yes | name |
| `Prenom`, `Postnom`, `Position` | yes | title, post nominal, position (or "Guest of …") |
| `Guest Name` | yes | the host's list of guests |
| `Phone`, `Email 1` | yes | phones, emails |
| `Email 2` | no | emails |

`Organisation`, `Comments`, `Dietary` and `Accessibility` have constants in `ProcessedGuestListParser.Headers`
but aren't read yet.

## Recipes

Every recipe ends the same way: update or add a test, then run `dotnet test`.

### 1. A column was renamed in the RSVP export

Say the ticketing system renames `Position title` to `Job title`.

1. In [SourceRsvpParser.cs](../src/GalaReconciler.Core/Parsing/SourceRsvpParser.cs), change the constant:
   ```csharp
   public const string PositionTitle = "Job title";
   ```
2. In [SourceSheet.cs](../tests/GalaReconciler.Tests/Helpers/SourceSheet.cs), rename it in `Headers` and in
   the matching `Set("…", …)` call in `ToRow`, so test workbooks look like the new export.
3. `dotnet test`.

Watch out for:
- **Capitals matter** in this file. `Job Title` and `Job title` are different headers.
- `Ticket type` and `Order id` are also how the app **recognises** the RSVP export
  ([WorkbookDetector.cs](../src/GalaReconciler.Core/Parsing/WorkbookDetector.cs)). It uses the same constants,
  so renaming them is still one change, but test it with a real file.
- If the export ever has **both** old and new names (different versions), make the lookup try both. In
  `ResolveColumns`: `PositionTitle: map.Find("Job title") ?? map.Find("Position title")`.

### 2. A column was renamed in the master list

Say `Phone` becomes `Mobile`.

1. In [ProcessedGuestListParser.cs](../src/GalaReconciler.Core/Parsing/ProcessedGuestListParser.cs), change the
   constant:
   ```csharp
   public const string Phone = "Mobile";
   ```
   The highlighter uses the same constant, so the yellow marks move with it.
2. In [ProcessedSheet.cs](../tests/GalaReconciler.Tests/Helpers/ProcessedSheet.cs), rename it in `Headers` and
   in `ToRow`.
3. Day columns are different: they're in `EventDays.All` in
   [EventDay.cs](../src/GalaReconciler.Core/Domain/EventDay.cs) (the `ProcessedHeader` value). Change, for example,
   `"THURS"` to `"THU"` there.
4. `dotnet test`.

### 3. Make a column required or optional

Each parser has a `RequiredHeaders` array near the top. If a header in it is missing, the app stops with
*"missing required column(s): …"*.

- **To make one optional:** remove it from `RequiredHeaders`. `map.Find(...)` then returns `null` when the
  column isn't there, and `CellText.Read(row, null)` returns `""`, so the value is simply blank. In the RSVP
  parser, columns read through the local `Find(...)` helper in `ResolveColumns` are typed `int` and assume the
  column exists. Switch that one to `map.Find(...)` and make its field in the `Columns` record `int?`.
- **To make one required:** add it to `RequiredHeaders`.

Don't make a header optional if `WorkbookDetector` uses it to recognise the file (`Ticket type`, `Order id`,
`Surname`, `Guest Name`, the day columns).

### 4. Change how a value is cleaned before comparing

All cleaning is in `src/GalaReconciler.Core/Normalization/`. Test cases are in
[NormalizerTests.cs](../tests/GalaReconciler.Tests/Normalization/NormalizerTests.cs).

| Class | What it does | Common changes |
|---|---|---|
| `TextNormalizer` | Title, post nominal, position. `Clean` turns blanks and placeholders into "no value". `ComparisonKey` ignores case, full stops and spaces. | Add a word to `Placeholders` (e.g. `"-"` is already covered, `"pending"` isn't). |
| `PhoneNormalizer` | Keeps digits only, turns `+61` into `0`, puts back a leading `0` that Excel dropped, splits cells on `/ , ; \|` and "or". | Support another country prefix; add a separator to `Separators()`. |
| `EmailNormalizer` | Lower-cases and splits cells holding several addresses. | |
| `NameNormalizer` | Builds the match key from first + last name, stripping leading titles (`Titles`: mr, mrs, ms, miss, dr, prof, sir, dame). | Add a title to `Titles` (lower case, e.g. `"hon"`) so "Hon Jane Smith" matches "Jane Smith". |

A change here affects **both files and both apps**, so it's the right place for "these two values should count
as the same".

### 5. Change the day / attendance logic

There are four places, one per step:

| Step | Where | What |
|---|---|---|
| Day names in each file | `EventDays.All` ([EventDay.cs](../src/GalaReconciler.Core/Domain/EventDay.cs)) | ticket label, master list header, short label |
| RSVP ticket → day + answer | [TicketTypeParser.cs](../src/GalaReconciler.Core/Parsing/TicketTypeParser.cs) | `"<day> attending"` / `"<day> not attending"`, built from `EventDays.All` |
| Master list cell → answer | `ProcessedGuestListParser.TryParseDayCell` | `Y`/`Yes`, `N`/`No`, `1`, blank. Anything else gives a warning and counts as not invited |
| Which pairs of answers differ | `DatesMismatchRule.Differs` ([DatesMismatchRule.cs](../src/GalaReconciler.Core/Rules/DatesMismatchRule.cs)) | the table in the README's "What is compared" |

Example: to accept `M` ("maybe") in the master list as "invited, no response yet", add this to
`TryParseDayCell` before the number check:

```csharp
if (text.Equals("M", StringComparison.OrdinalIgnoreCase))
{
    state = AttendanceState.AwaitingResponse;
    return true;
}
```

Then add a case to `ProcessedGuestListParserTests` and update the README table.

### 6. Compare a new field (worked example: Organisation)

Both files have an `Organisation` column that isn't compared yet. Here's the full path to compare it. Copy this
pattern for any other single-text field. For a field holding several values (like phones), derive the rule from
`SetMismatchRule` instead of `TextMismatchRule`.

**a. Hold the value on the guest.** In [GuestRecord.cs](../src/GalaReconciler.Core/Domain/GuestRecord.cs):
```csharp
public string? Organisation { get; set; }
```

**b. Read it from the RSVP export.** In
[SourceRsvpParser.cs](../src/GalaReconciler.Core/Parsing/SourceRsvpParser.cs):
1. Add `public const string Organisation = "Organisation";` to `Headers`.
2. Add `int? Organisation` to the `Columns` record, and `Organisation: map.Find(Headers.Organisation)` in
   `ResolveColumns`.
3. Add `string? Organisation` to the `Ticket` record, and pass `TextNormalizer.Clean(Cell(columns.Organisation))`
   in the `new Ticket(...)` call in `ReadSheet` (same position as in the record).
4. In `BuildRecords`, next to `Position = …`:
   ```csharp
   Organisation = personTickets.Select(t => t.Organisation).FirstOrDefault(t => t is not null),
   ```

**c. Read it from the master list.** In
[ProcessedGuestListParser.cs](../src/GalaReconciler.Core/Parsing/ProcessedGuestListParser.cs) (the
`Organisation` header constant already exists):
1. `var organisationColumn = map.Find(Headers.Organisation);` next to the other `Find` calls.
2. `Organisation = TextNormalizer.Clean(Cell(organisationColumn)),` in the `new GuestRecord { … }`.
3. In `Merge`: `target.Organisation ??= duplicate.Organisation;`.
4. Add it to `RequiredHeaders` only if every master list will have it.

**d. Name the new difference.** In [DiscrepancyType.cs](../src/GalaReconciler.Core/Domain/DiscrepancyType.cs),
add `OrganisationMismatch` to the enum (at the end) and `"Organisation mismatch"` to `ToDisplay`.

**e. Write the rule.** Add to [TextMismatchRule.cs](../src/GalaReconciler.Core/Rules/TextMismatchRule.cs):
```csharp
/// <summary>Organisation, compared with the master list's Organisation.</summary>
public sealed class OrganisationMismatchRule : TextMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.OrganisationMismatch;

    protected override string? SelectValue(GuestRecord record) => record.Organisation;
}
```
Override `ShouldCompare` to skip some pairs, as `PositionMismatchRule` does for "Guest of …" rows.

**f. Turn it on.** In
[ComparisonServiceCollectionExtensions.cs](../src/GalaReconciler.Core/ComparisonServiceCollectionExtensions.cs):
```csharp
services.AddSingleton<IDiscrepancyRule, OrganisationMismatchRule>();
```
Both apps now find the difference. The rest is about showing it.

**g. Console app output.**
- [ProcessedFileHighlighter.cs](../src/GalaReconciler.Cli/ProcessedFileHighlighter.cs), in `Marks`:
  ```csharp
  case DiscrepancyType.OrganisationMismatch:
      yield return (Headers.Organisation, ChangedFill, ValuesNote(d, "Organisation"));
      break;
  ```
- [ExcelReportWriter.cs](../src/GalaReconciler.Cli/ExcelReportWriter.cs): give it colours in `StyleType`.

**h. Web app output.**
- `Results.cshtml`: `BadgeClasses`, `DotClasses`, `CardDescription`. `Runs.cshtml`: `CountClasses`.
- Saved runs keep a count per type: add `OrganisationMismatchCount` to `Persistence/Run.cs` (and `Run.CountOf`),
  set it in `RunStore.CompleteAsync`, add it to the `Type` filter in `RunStore.ListAsync`, then add a migration:
  ```bash
  dotnet tool restore
  dotnet ef migrations add AddOrganisationMismatch --project src/GalaReconciler.Web --output-dir Features/Reconciliation/Persistence/Migrations
  ```

**i. Tests and docs.**
- Add `Organisation` to `SourceTicket` / `ProcessedGuest` in the test helpers if it isn't settable yet, and set
  it in `ToRow`.
- Add a parser test for each file and a rule test in
  [RuleTests.cs](../tests/GalaReconciler.Tests/Rules/RuleTests.cs).
- Add a row to the README's "What is compared" table.

To find every place a type needs handling, search for an existing one, e.g. `PositionMismatch`. Every hit is a
place your new type probably needs a line too.

### 7. Change how people are matched

[GuestMatcher.cs](../src/GalaReconciler.Core/Matching/GuestMatcher.cs) does it in order:

1. **Name**: the normalized "first last" key (`NameNormalizer.Key`). Changing how names are normalized changes
   matching too.
2. **Email**: only when exactly one unmatched person on each side shares the address.
3. **Leftovers** are "Guest not found". A name within `PossibleMatchMaxDistance` (2) letters on the other side is
   suggested, never matched.

Phones are deliberately never used to match, because people share numbers. Add new matching steps between 2 and 3,
and keep the "exactly one on each side" safeguard so one shared value can't pair the wrong people.

### 8. Add a warning

Warnings don't stop the comparison. They appear on the Warnings sheet and the web results page. In either parser,
add to the `warnings` list:

```csharp
warnings.Add(new(WarningFile, $"{sheet.Name} row {rowNumber}: <what's odd and what the app did about it>."));
```

Say what the app did ("…was skipped", "…treated as not invited") so the user knows whether to act.

## Development loop

```bash
dotnet test
```

Runs everything. While working on one area, run just its tests:

```bash
dotnet test --filter "FullyQualifiedName~SourceRsvpParserTests"
```

Other useful filters (`~` means "name contains"): `ProcessedGuestListParserTests`, `RuleTests` (every rule),
`NormalizerTests` (every normalizer), `GuestMatcherTests`, `ProcessedFileHighlighterTests`, `ExcelReportWriterTests`,
`CliAppTests`.

Try a change on the sample files (fictional data; created by the test run):

```bash
dotnet run --project src/GalaReconciler.Cli -- --folder samples --no-pause
```

Try it on a real file without copying the exe around:

```bash
dotnet run --project src/GalaReconciler.Cli -- "C:\path\to\RSVP export.xlsx" "C:\path\to\Master list.xlsx" --no-pause
```

**How the tests build workbooks.** Tests never use real files. They build workbooks in memory with
`WorkbookBuilder`, `SourceSheet` and `ProcessedSheet` in `tests/GalaReconciler.Tests/Helpers/`. Those helpers'
`Headers` lists mirror the real files' columns, so **keep them in step with the real files**. A typical parser
test:

```csharp
var result = Parse(new SourceTicket("Saturday attending", "Marco", "Bellini") { PositionTitle = "CEO" });
Assert.Equal("CEO", Assert.Single(result.Records).Position);
```

`SampleWorkbookBuilder` builds the `samples/` files from fictional guests in `TestGuests`. Add a guest there
when you want a new case to show up in the samples.

## Finding things fast

| Looking for | Search for |
|---|---|
| Every header the app reads | `public const string` in `Core/Parsing/` |
| Where a column is read | the header constant name, e.g. `Headers.Phone` |
| Everything a difference type touches | its enum name, e.g. `EmailMismatch` |
| Where a warning message comes from | a few words of the message |
| Where an error message comes from | `WorkbookFormatException` / `InputFileException` |

From a terminal:

```bash
git grep -n "Headers.Phone"
```

In Visual Studio, **Ctrl+Shift+F** searches the solution; **F12** on a name goes to its definition and
**Shift+F12** lists every use.

## Before you ship

1. `dotnet test` passes.
2. Run the console app on the client's real files (or their latest copies) and look at the Warnings sheet for
   anything new.
3. Update the README tables if what's compared or which columns are needed has changed.
4. Publish the exe:
   ```bash
   dotnet publish src/GalaReconciler.Cli -p:PublishProfile=win-x64
   ```
   Send `artifacts/win-x64/GalaReconciler.exe` (about 40 MB) and `HOW TO USE.txt`.
