# GALA Reconciler

Compares the ticketing system's **RSVP export** (Source) with the hand-maintained **master guest list**
(Processed) and lists the differences in:

- which days each person and their guests are invited to and attending (Thu, Fri, Sat, Sun, Grid Gala)
- mobile numbers and email addresses
- title, post nominal and position title
- dietary and accessibility requirements (console app only for now)

It comes in two forms that share the same comparison code:

- **Console app (`GalaReconciler.exe`)**: put it in a folder with the three files (the RSVP export, the
  separate **Gala RSVP export** and the master list) and run it. It works out which file is which and saves
  two new files next to them: a results workbook and a highlighted copy of the master list. The original
  files are never changed.
- **Web app**: upload the two files in a browser. Each comparison runs in the background, is saved as a
  run in a local SQLite database (results only), and the run page updates over SignalR. The Runs page lists
  past runs with filters. The web app still takes a single RSVP export, with Grid Gala as "Grid Gala
  attending" tickets in it; it doesn't take the separate Gala RSVP export.

Both read the uploaded workbooks in memory only and never store the input files.

Changing the code? The [developer guide](docs/DEVELOPER-GUIDE.md) shows where everything is and has step-by-step
recipes for renaming columns, reading new columns, changing comparison logic and adding checks.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) (only for the web app's client assets; the tests build the web app too)

People who only use the published `GalaReconciler.exe` need nothing installed.

## Console app

### For users

Download `GalaReconciler-win-x64` from the latest run of the **Console app** workflow on GitHub
(Actions tab → the run → Artifacts), or the zip on a release. It contains `GalaReconciler.exe` and
`HOW TO USE.txt`.

1. Put `GalaReconciler.exe` in the folder with the RSVP export, the Gala RSVP export and the master guest
   list (any file names).
2. Double-click it.
3. Two files are saved in the same folder, and the window waits for a key press so the summary can be read:
   - `Guest List Reconciliation <date> <time>.xlsx`: the results
   - `Guest List Reconciliation <date> <time> - <master list name> (highlighted).xlsx`: the highlighted copy

   Re-running never overwrites earlier files; both get the next "(2)", "(3)"… number together.

The **highlighted copy** is the master list exactly as it was (other sheets, hidden columns and formatting
kept) with every difference marked on its own row:

| Mark | Meaning |
|---|---|
| Yellow cell | The value differs from the RSVP export (the Differences sheet in the results gives both values). Marked cells: the day columns, Prenom, Postnom, Position, Phone, Email 1/Email 2, Dietary, Accessibility. |
| Red First Name and Surname | A main guest who isn't in either RSVP export (the results give a possible match if there's a similar name). Anyone who is a guest of someone is never marked as missing. |

"Guest not found" lists **main guests only** in the console app: anyone who is a guest of someone (a "Guest of …" Position, listed in a Guest Name cell, a ticket bought by another attendee, or an extra ticket under the host's name) is left out. Guests who are found are still compared.

People only in the exports have no row to mark; they're listed in the results as "Guest not found". The Details column
says where they were found: "Only in RSVP export", "Only in Gala RSVP export" or "In both RSVP exports, not in the
master list". People in the master list but neither export say "Not in either RSVP export".

Before someone is reported, the app tries, in order: their name against the master list's First Name + Surname and every name in the Guest Name column (titles such as "Mrs" are ignored, and one cell can hold several guests); the name in columns B/C if the attendee name differs;
a shared email (only when exactly one person on each side has it); and, for a second ticket under the same name on one
order, a guest the master list names for that host. If the two exports spell a person differently but give the same
email and nearly the same name, they count as one person and the Warnings sheet says so.

The **results workbook** has three sheets:

| Sheet | Contents |
|---|---|
| Summary | File names (including the highlighted copy), guest counts and the number of each type of difference. |
| Differences | One row per difference in a filterable Excel table: type, guest, host, details, both values, a day grid (Source → Processed, differing days highlighted) and the sheet/row location in each file. |
| Warnings | Unrecognised ticket types or day values, duplicate rows and other oddities found while reading. |

### What is compared

| | RSVP export (Source) | Master list (Processed) | Flagged when |
|---|---|---|---|
| Days | a ticket "… attending" / "… not attending" per day, or no ticket | `Y` invited and coming, `N` invited and not coming, `1` invited with no response yet, blank (or anything else) not invited | "attending" isn't `Y`; "not attending" isn't `N`; no ticket but `Y` (or `N` with `--strict`). No ticket matches blank or `1`. |
| Grid Gala | the **Gala RSVP export**: "RSVP - Attending" / "RSVP - Not attending", or no ticket | Grid Gala, as for the days | "RSVP - Attending" isn't `Y`; "RSVP - Not attending" isn't `N`; no RSVP but `Y` (or `N` with `--strict`). |
| Title | "Please specify your title", else Title | Prenom | different |
| Post nominal | "Please specify your post nominal", else Post nominal | Postnom | different |
| Position | Position title | Position (rows saying "Guest of …" are skipped) | different |
| Mobile | Mobile + Telephone (all of a person's rows) | Phone (several numbers on separate lines are fine) | no number in common (one match is enough) |
| Email | Email | Email 1 + Email 2 | no address in common (the address in either column is enough) |
| Dietary *(console app)* | every column with "dietary" in its header, in both exports (e.g. "Dietary requirements" + "Please specify any dietary requirements") | Dietary | a requirement from the exports isn't mentioned in Dietary |
| Accessibility *(console app)* | every column with "accessibility" in its header, in both exports | Accessibility | a requirement from the exports isn't mentioned in Accessibility |

For dietary and accessibility, all of a person's answers (every ticket, both exports, the drop-down and the
"Please specify" text) are collected and split on commas, semicolons and line breaks. "Other", "Yes", "None",
"Nil", "N/A" and similar aren't requirements and are dropped. Each remaining part must appear somewhere in the
master list's cell, ignoring case, spaces and punctuation ("Gluten free" = "Gluten-free"); extra detail in the master
list is fine, and nothing in the exports raises nothing. An unnamed guest's extra ticket isn't used, because its
answers could be the host's or the guest's.

For title, post nominal and position, a blank or `nil` value in the export never raises anything (the
master list is left as it is), and case, full stops and spacing are ignored (`Mr.` = `Mr`, `O.A.M.` = `OAM`).
A drop-down value of "Other" with nothing specified counts as blank. The titles Miss and Ms count as the same.

The Gala RSVP export is matched to people the same way as the RSVP export (by name, then by a shared email), and
its rows show in the results as `<sheet> (Gala RSVP) · row …`. Its columns are like the RSVP export's, but only
`Ticket type`, `Order id`, `First name` and `Last name` are required; the attendee name, phones, email, title and
position are used when they're there.

How the files are found: every `.xlsx` in the folder is opened (except Excel's `~$` lock files and earlier
`Guest List Reconciliation…` reports, and older `GALA Reconciliation…` ones). Both exports have `Ticket type` and `Order id` columns: the one whose ticket types
are mostly "RSVP - Attending" / "RSVP - Not attending" is the Gala RSVP export, the other is the RSVP export. The
one whose first sheet has `Surname`, `Guest Name` and the day columns is the master list. If any of the three is
missing, or there's more than one candidate, the app says so instead of guessing.

From a command prompt (the three file names can be given in any order):

```
GalaReconciler [source.xlsx gala.xlsx processed.xlsx] [--folder <folder>] [--output <file or folder>] [--strict] [--no-pause]
```

Exit codes: `0` report written, `1` a problem with the input files or arguments, `2` unexpected error.

### Run it from source

```bash
dotnet run --project src/GalaReconciler.Cli -- --folder samples
```

### Publish the exe

```bash
dotnet publish src/GalaReconciler.Cli -p:PublishProfile=win-x64
```

This writes a self-contained, single-file `GalaReconciler.exe` (about 40 MB, no .NET install needed) and
`HOW TO USE.txt` to `artifacts/win-x64/`.

The GitHub Actions workflow `.github/workflows/console-app.yml` does this on every push to `main` and every
pull request (after running the tests) and attaches the result to the run. Pushing a version tag such as
`v1.2.0` also creates a GitHub Release with a zip of the exe, stamped with that version:

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Web app

```bash
dotnet run --project src/GalaReconciler.Web
```

Then open http://localhost:5077. The database is created (and migrated) on startup.

`dotnet build` runs `npm install` the first time, then `npm run build`, which:

- builds Tailwind CSS into `wwwroot/css/site.css`
- copies the SignalR browser client into `wwwroot/lib/signalr/`

To build without Node, pass `-p:SkipClientAssets=true` (both files must then already exist).

While working on views, keep the CSS rebuilding in a second terminal:

```bash
cd src/GalaReconciler.Web
npm run watch:css
```

### Configuration

`src/GalaReconciler.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "Reconciliation": "Data Source=App_Data/gala.db"
},
"Reconciliation": {
  "StrictNotAttending": false,
  "RetentionDays": 90,
  "MaxQueuedComparisons": 10
}
```

| Setting | Meaning |
|---|---|
| `ConnectionStrings:Reconciliation` | SQLite database for saved runs. A relative path is relative to the web project folder. |
| `StrictNotAttending` | By default a day with no ticket in the RSVP export is fine against `N` in the master list (the guest may have declined outside the ticketing system). `true` flags it. (The console app's `--strict` flag does the same.) |
| `RetentionDays` | Runs older than this are deleted automatically (checked at startup and every 6 hours). `0` keeps them forever. |
| `MaxQueuedComparisons` | How many uploads can wait for the worker. Each holds both files in memory (up to ~20 MB), and further uploads get a "server is busy" message. |

> **Personal data:** saved runs contain guests' names, phone numbers and emails, and the web app has no
> login. Anyone who can reach it can see every saved run. Keep it on a trusted machine or network, or add
> authentication before hosting it more widely.

### How it works

```
Upload (POST /Reconciliation/Compare)
  → validate, read both files into memory
  → save a Queued run, put the files on ComparisonQueue
  → redirect to /runs/{id}

ComparisonWorker (background, one comparison at a time)
  → ComparisonProcessor: CompareFilesHandler (parse → match → rules)
  → RunStore saves results (or the error) → Completed / Failed
  → SignalR (ComparisonHub) tells the run page, which reloads to show the results
```

Comparisons run one at a time, which caps peak memory at a single comparison (about 0.5 GB for a 10 MB
RSVP export). Runs that were still queued or processing when the app stopped are marked as interrupted on
the next startup; their files were only in memory, so they need uploading again. If the browser can't
connect to SignalR, the run page falls back to reloading itself every few seconds.

### Changing the database schema

After changing the entities in `Features/Reconciliation/Persistence/`, add a migration (the `dotnet-ef`
tool is in the repository's tool manifest):

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/GalaReconciler.Web --output-dir Features/Reconciliation/Persistence/Migrations
```

It's applied automatically the next time the app starts.

## Test

```bash
dotnet test
```

Tests that start the web app use a throwaway SQLite file in the temp folder; console app tests use temp
folders. The test run also writes `samples/Source.xlsx`, `samples/Gala.xlsx` and `samples/Processed.xlsx`
(fictional data, from `tests/GalaReconciler.Tests/Helpers/SampleWorkbookBuilder.cs`) for trying the console app.
If the samples are open in Excel, set `GALA_SAMPLES_DIR` to write them to another folder.

## Project layout

```
src/GalaReconciler.Core/           the comparison, shared by both apps
  CompareFilesHandler.cs           parse → match → rules
  ComparisonServiceCollectionExtensions.cs   registers parsers, matcher and rules (AddComparison)
  Domain/                          EventDay (+ EventDays mapping), GuestRecord, Discrepancy, …
  Parsing/                         SourceRsvpParser, ProcessedGuestListParser, HeaderMap, TicketTypeParser, WorkbookDetector
  Normalization/                   NameNormalizer, PhoneNormalizer, EmailNormalizer
  Matching/                        GuestMatcher (name, other names, unique shared email, extra tickets; never phone)
  Rules/                           IDiscrepancyRule + DatesMismatch, MobileNumberMismatch, EmailMismatch,
                                   TextMismatchRule (Title, PostNominal, Position), and
                                   RequirementsMismatchRule (Dietary, Accessibility; console app only, AddRequirementChecks)

src/GalaReconciler.Cli/            console app → GalaReconciler.exe
  CliApp.cs                        find files → compare → save report
  InputFileFinder.cs               picks the three workbooks out of a folder
  ExcelReportWriter.cs             results workbook: Summary / Differences / Warnings
  ProcessedFileHighlighter.cs      highlighted copy of the master list (yellow cells, red names)
  Properties/PublishProfiles/win-x64.pubxml   single-file publish settings

src/GalaReconciler.Web/            web app
  Features/Reconciliation/         one vertical slice: controller, view models, views
    Persistence/                   ReconciliationDbContext, Run entities, RunStore, Migrations/
    Background/                    ComparisonQueue, ComparisonWorker, ComparisonProcessor, RunRetentionService
    Realtime/                      ComparisonHub (SignalR) and the notifier the worker uses
  Features/Error/                  friendly error page
  Shared/Views/                    _Layout, Error
  Shared/Infrastructure/           FeatureViewLocationExpander (Features/{Controller}/{View}.cshtml, then Shared/Views/)
  Styles/app.css                   Tailwind v4 input

tests/GalaReconciler.Tests/        xUnit tests for all three projects
```

`_ViewImports.cshtml` and `_ViewStart.cshtml` sit at the web project root rather than in `Shared/Views/`.
Razor finds those two files by walking up the folders from each view, not through the view location
expander, so they have to sit above both `Features/` and `Shared/`.

## Adding a new rule

1. Create a class in `src/GalaReconciler.Core/Rules/` that implements `IDiscrepancyRule`. For a
   set-of-values comparison like phones or emails, derive from `SetMismatchRule`; for a single text field
   like title, derive from `TextMismatchRule`.
2. Add a value to `DiscrepancyType` (and its label in `DiscrepancyTypeExtensions.ToDisplay`).
3. Register it in `ComparisonServiceCollectionExtensions.AddComparison` (both apps pick it up):
   ```csharp
   services.AddSingleton<IDiscrepancyRule, MyNewRule>();
   ```
   Rules run in registration order.
4. Give the new type colours in the web views (`Results.cshtml`: `BadgeClasses`, `DotClasses`,
   `CardDescription`; `Runs.cshtml`: `CountClasses`) and in `ExcelReportWriter.StyleType`. Say which
   master-list column to mark in `ProcessedFileHighlighter.Marks`.
5. Saved web runs keep a count per type for the runs list: add a `…Count` column to `Run` (set it in
   `RunStore.CompleteAsync`, and add it to `Run.CountOf` and the `Type` filter in `RunStore.ListAsync`),
   then add a migration.

## Adding a new event day

1. Add a value to the `EventDay` enum in `src/GalaReconciler.Core/Domain/EventDay.cs`.
2. Add one entry to `EventDays.All` in the same file, giving the ticket type prefix (e.g. `"Monday"` for
   `"Monday attending"`), the Processed column header (e.g. `"MON"`) and a short label for the day grid.

The ticket type parser, the Processed parser (including its required headers), the dates rule, the web
day grid and the report's day columns all read from that list. Saved runs store each difference's days as
JSON, so no migration is needed.
