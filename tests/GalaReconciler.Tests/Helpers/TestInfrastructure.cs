using System.Text.RegularExpressions;
using GalaReconciler.Core;
using GalaReconciler.Core.Matching;
using GalaReconciler.Core.Parsing;
using GalaReconciler.Core.Rules;
using GalaReconciler.Web.Features.Reconciliation;
using GalaReconciler.Web.Features.Reconciliation.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GalaReconciler.Tests.Helpers;

/// <summary>The real app, with its SQLite database in a throwaway temp file.</summary>
public sealed class GalaWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"gala-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting($"ConnectionStrings:{ReconciliationServiceCollectionExtensions.ConnectionStringName}", $"Data Source={_databasePath}");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // Best effort; it's in the temp folder.
        }
    }
}

/// <summary>A migrated in-memory SQLite database that lives as long as this object.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public DbContextOptions<ReconciliationDbContext> Options =>
        new DbContextOptionsBuilder<ReconciliationDbContext>().UseSqlite(_connection).Options;

    public ReconciliationDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>A clock the test controls.</summary>
public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

public static partial class TestHandlers
{
    /// <summary>The handler as the web app registers it: the shared rules, default options.</summary>
    public static CompareFilesHandler CreateCompareFilesHandler() =>
        new ServiceCollection().AddComparison().BuildServiceProvider().GetRequiredService<CompareFilesHandler>();

    /// <summary>The handler as the console app registers it: the shared rules plus dietary and accessibility.</summary>
    public static CompareFilesHandler CreateConsoleCompareFilesHandler() =>
        new ServiceCollection().AddComparison().AddRequirementChecks().BuildServiceProvider().GetRequiredService<CompareFilesHandler>();

    /// <summary>The comparison of the three sample workbooks, as the console app does it.</summary>
    public static ReconciliationResult CompareSamples()
    {
        using var source = SampleWorkbookBuilder.Source().ToStream();
        using var gala = SampleWorkbookBuilder.Gala().ToStream();
        using var processed = SampleWorkbookBuilder.Processed().ToStream();
        return CreateConsoleCompareFilesHandler().Handle(
            new CompareFilesRequest(source, "Source.xlsx", processed, "Processed.xlsx", gala, "Gala.xlsx"));
    }

    /// <summary>Posts both files through the real upload form and returns the run page URL it redirects to.</summary>
    public static async Task<string> UploadAsync(HttpClient client, Stream sourceFile, Stream processedFile)
    {
        var form = await client.GetStringAsync("/");
        var token = AntiforgeryToken().Match(form).Groups[1].Value;

        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StreamContent(sourceFile), "SourceFile", "Source.xlsx" },
            { new StreamContent(processedFile), "ProcessedFile", "Processed.xlsx" },
        };
        var response = await client.PostAsync("/Reconciliation/Compare", content);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!.OriginalString;
    }

    public static Task<string> UploadSamplesAsync(HttpClient client) =>
        UploadAsync(client, SampleWorkbookBuilder.SingleExportSource().ToStream(), SampleWorkbookBuilder.Processed().ToStream());

    /// <summary>Polls the run page until the background worker has finished with it.</summary>
    public static async Task<string> WaitForRunAsync(HttpClient client, string runUrl)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var html = await client.GetStringAsync(runUrl);
            if (!html.Contains("data-status-label"))
            {
                return html;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Run {runUrl} didn't finish.");
            }

            await Task.Delay(100);
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
