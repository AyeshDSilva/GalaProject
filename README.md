# GALA Reconciler

Compares the ticketing system's **RSVP export** (Source) with the hand-maintained **master guest list**
(Processed) and lists the differences in:

- which days each person and their guests are invited to and attending (Thu, Fri, Sat, Sun, Grid Gala)
- mobile numbers and email addresses
- title, post nominal and position title

It comes in two forms that share the same comparison code:

- **Console app (`GalaReconciler.exe`)**: put it in a folder with the two files and run it. It works out
  which file is which and saves two new files next to them: a results workbook and a highlighted copy of
  the master list. The original files are never changed.
- **Web app**: upload the two files in a browser. Each comparison runs in the background, is saved as a
  run in a local SQLite database (results only), and the run page updates over SignalR. The Runs page lists
  past runs with filters.

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

1. Put `GalaReconciler.exe` in the folder with the RSVP export and the master guest list (any file names).
2. Double-click it.
3. Two files are saved in the same folder, and the window waits for a key press so the summary can be read:
   - `GALA Reconciliation <date> <time>.xlsx`: the results
   - `GALA Reconciliation <date> <time> - <master list name> (highlighted).xlsx`: the highlighted copy

   Re-running never overwrites earlier files; both get the next "(2)", "(3)"… number together.

The **highlighted copy** is the master list exactly as it was (other sheets, hidden columns and formatting
kept) with every difference marked on its own row:

| Mark | Meaning |
|---|---|
| Yellow cell with a note | The value differs from the RSVP export. The note gives both values (or, for a day, both answers). Marked cells: the day columns, Prenom, Postnom, Position, Phone, Email 1/Email 2. |
| Red First Name and Surname | The person isn't in the RSVP export. The note gives a possible match if there's a similar name. A `guest` in Guest Name (name not known yet) is never reported as missing; it's only compared when the export has an extra ticket for it. |

People only in the RSVP export have no row to mark; they're listed in the results as "Guest not found".

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
| Title | "Please specify your title", else Title | Prenom | different |
| Post nominal | "Please specify your post nominal", else Post nominal | Postnom | different |
| Position | Position title | Position (rows saying "Guest of …" are skipped) | different |
| Mobile | Mobile + Telephone (all of a person's rows) | Phone (several numbers on separate lines are fine) | no number in common (one match is enough) |
| Email | Email | Email 1 + Email 2 | no address in common (the address in either column is enough) |

For title, post nominal and position, a blank or `nil` value in the export never raises anything (the
master list is left as it is), and case, full stops and spacing are ignored (`Mr.` = `Mr`, `O.A.M.` = `OAM`).
A drop-down value of "Other" with nothing specified counts as blank. The titles Miss and Ms count as the same.

How the files are found: every `.xlsx` in the folder is opened (except Excel's `~$` lock files and earlier
`GALA Reconciliation…` reports). The one with `Ticket type` and `Order id` columns is the RSVP export, and the
one whose first sheet has `Surname`, `Guest Name` and the day columns is the master list. If either is missing,
or there's more than one candidate, the app says so instead of guessing.

From a command prompt:

```
GalaReconciler [source.xlsx processed.xlsx] [--folder <folder>] [--output <file or folder>] [--strict] [--no-pause]
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
folders. The test run also writes `samples/Source.xlsx` and `samples/Processed.xlsx` (fictional data, from
`tests/GalaReconciler.Tests/Helpers/SampleWorkbookBuilder.cs`) for trying either app.

## Project layout

```
src/GalaReconciler.Core/           the comparison, shared by both apps
  CompareFilesHandler.cs           parse → match → rules
  ComparisonServiceCollectionExtensions.cs   registers parsers, matcher and rules (AddComparison)
  Domain/                          EventDay (+ EventDays mapping), GuestRecord, Discrepancy, …
  Parsing/                         SourceRsvpParser, ProcessedGuestListParser, HeaderMap, TicketTypeParser, WorkbookDetector
  Normalization/                   NameNormalizer, PhoneNormalizer, EmailNormalizer
  Matching/                        GuestMatcher (name, then unique shared email; never phone)
  Rules/                           IDiscrepancyRule + DatesMismatch, MobileNumberMismatch, EmailMismatch,
                                   and TextMismatchRule (Title, PostNominal, Position)

src/GalaReconciler.Cli/            console app → GalaReconciler.exe
  CliApp.cs                        find files → compare → save report
  InputFileFinder.cs               picks the two workbooks out of a folder
  ExcelReportWriter.cs             results workbook: Summary / Differences / Warnings
  ProcessedFileHighlighter.cs      highlighted copy of the master list (yellow cells with notes, red names)
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
