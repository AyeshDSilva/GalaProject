# GALA Reconciler

Compares the ticketing system's **RSVP export** (Source) with the hand-maintained **master guest list**
(Processed) and lists the differences in:

- which days each person and their guests are attending (Thu, Fri, Sat, Sun, Grid Gala)
- mobile numbers
- email addresses

Each comparison is saved as a **run**. Uploading the files queues the run, a background worker compares
them, and the run page updates itself over SignalR when the results are ready. The **Runs** page lists past
runs and can filter them by guest name, file name, type of difference, status and date.

Uploaded files are held in memory only (never written to disk) and are discarded once processed. Only the
results are stored, in a local SQLite database.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) (used only to build the client assets)

## Run

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

## Test

```bash
dotnet test
```

Tests that start the web app use a throwaway SQLite file in the temp folder. The test run also writes
`samples/Source.xlsx` and `samples/Processed.xlsx` (from
`tests/GalaReconciler.Tests/Helpers/SampleWorkbookBuilder.cs`). Upload those two files to try the app.

## Configuration

`appsettings.json`:

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
| `StrictNotAttending` | By default "not attending" and "no response" count as the same thing. `true` also flags days where one file says N and the other is blank. |
| `RetentionDays` | Runs older than this are deleted automatically (checked at startup and every 6 hours). `0` keeps them forever. |
| `MaxQueuedComparisons` | How many uploads can wait for the worker. Each holds both files in memory (up to ~20 MB), and further uploads get a "server is busy" message. |

> **Personal data:** saved runs contain guests' names, phone numbers and emails, and the app has no login.
> Anyone who can reach the app can see every saved run. Keep it on a trusted machine or network, or add
> authentication before hosting it more widely.

## How it works

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

```
src/GalaReconciler.Web/
  Features/Reconciliation/         one vertical slice: controller, view models, views and logic
    ReconciliationController.cs    upload form, Compare, runs list, run page, delete
    CompareFilesHandler.cs         parse → match → rules
    Domain/                        EventDay (+ EventDays mapping), GuestRecord, Discrepancy, …
    Parsing/                       SourceRsvpParser, ProcessedGuestListParser, HeaderMap, TicketTypeParser
    Normalization/                 NameNormalizer, PhoneNormalizer, EmailNormalizer
    Matching/                      GuestMatcher (name, then unique shared email; never phone)
    Rules/                         IDiscrepancyRule + DatesMismatch, MobileNumberMismatch, EmailMismatch
    Persistence/                   ReconciliationDbContext, Run entities, RunStore, Migrations/
    Background/                    ComparisonQueue, ComparisonWorker, ComparisonProcessor, RunRetentionService
    Realtime/                      ComparisonHub (SignalR) and the notifier the worker uses
  Features/Error/                  friendly error page
  Shared/Views/                    _Layout, Error
  Shared/Infrastructure/           FeatureViewLocationExpander (Features/{Controller}/{View}.cshtml, then Shared/Views/)
  Styles/app.css                   Tailwind v4 input
```

`_ViewImports.cshtml` and `_ViewStart.cshtml` sit at the project root rather than in `Shared/Views/`.
Razor finds those two files by walking up the folders from each view, not through the view location
expander, so they have to sit above both `Features/` and `Shared/`.

## Changing the database schema

After changing the entities in `Features/Reconciliation/Persistence/`, add a migration (the `dotnet-ef`
tool is in the repository's tool manifest):

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/GalaReconciler.Web --output-dir Features/Reconciliation/Persistence/Migrations
```

It's applied automatically the next time the app starts.

## Adding a new rule

1. Create a class in `Features/Reconciliation/Rules/` that implements `IDiscrepancyRule`. For a
   set-of-values comparison like phones or emails, derive from `SetMismatchRule` instead.
2. Add a value to `DiscrepancyType` (and its label in `DiscrepancyTypeExtensions.ToDisplay`).
3. Register it in `ReconciliationServiceCollectionExtensions.AddReconciliation`:
   ```csharp
   services.AddSingleton<IDiscrepancyRule, MyNewRule>();
   ```
   Rules run in registration order.
4. Give the new type colours in `Results.cshtml` (`BadgeClasses`, `DotClasses`, `CardDescription`) and
   `Runs.cshtml` (`CountClasses`).
5. Saved runs keep a count per type for the runs list: add a `…Count` column to `Run` (set it in
   `RunStore.CompleteAsync`, and add it to `Run.CountOf` and the `Type` filter in `RunStore.ListAsync`),
   then add a migration.

## Adding a new event day

1. Add a value to the `EventDay` enum in `Features/Reconciliation/Domain/EventDay.cs`.
2. Add one entry to `EventDays.All` in the same file, giving the ticket type prefix (e.g. `"Monday"` for
   `"Monday attending"`), the Processed column header (e.g. `"MON"`) and a short label for the day grid.

The ticket type parser, the Processed parser (including its required headers), the dates rule and the
results day grid all read from that list. Saved runs store each difference's days as JSON, so no
migration is needed.
