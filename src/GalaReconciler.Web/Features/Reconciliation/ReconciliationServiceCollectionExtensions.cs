using GalaReconciler.Core;
using GalaReconciler.Web.Features.Reconciliation.Background;
using GalaReconciler.Web.Features.Reconciliation.Persistence;
using GalaReconciler.Web.Features.Reconciliation.Realtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GalaReconciler.Web.Features.Reconciliation;

public static class ReconciliationServiceCollectionExtensions
{
    public const string ConnectionStringName = "Reconciliation";
    private const string DefaultConnectionString = "Data Source=App_Data/gala.db";

    public static IServiceCollection AddReconciliation(this IServiceCollection services, IConfiguration configuration)
    {
        // Both option classes read the same "Reconciliation" section: StrictNotAttending is a comparison
        // setting shared with the console app; the rest are web-only.
        var section = configuration.GetSection(ReconciliationOptions.SectionName);
        services.Configure<ReconciliationOptions>(section);
        services.Configure<ComparisonOptions>(section);

        // Parsers, matcher, rules and CompareFilesHandler, shared with the console app.
        services.AddComparison();

        // Saved runs (results only) in SQLite. The connection string is resolved when the context is
        // created so test hosts can override it.
        services.AddDbContext<ReconciliationDbContext>((provider, options) =>
            options.UseSqlite(ResolveConnectionString(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<IHostEnvironment>())));
        services.AddScoped<RunStore>();

        // Background processing and live updates.
        services.AddSingleton<ComparisonQueue>();
        services.AddScoped<ComparisonProcessor>();
        services.AddHostedService<ComparisonWorker>();
        services.AddSingleton<RunRetentionService>();
        services.AddHostedService(provider => provider.GetRequiredService<RunRetentionService>());
        services.AddSignalR();
        services.AddSingleton<IRunNotifier, SignalRRunNotifier>();

        return services;
    }

    /// <summary>Creates or upgrades the database, then fails runs a restart interrupted.</summary>
    public static async Task InitializeReconciliationAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>();

        var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (Path.GetDirectoryName(dataSource) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        await db.Database.MigrateAsync();

        var interrupted = await scope.ServiceProvider.GetRequiredService<RunStore>().FailInterruptedAsync(CancellationToken.None);
        if (interrupted > 0)
        {
            app.Logger.LogWarning("Marked {Count} interrupted runs as failed", interrupted);
        }
    }

    public static IEndpointRouteBuilder MapReconciliationHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<ComparisonHub>(ComparisonHub.Path);
        return endpoints;
    }

    /// <summary>A relative SQLite path is taken relative to the content root, not the working directory.</summary>
    private static string ResolveConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var builder = new SqliteConnectionStringBuilder(configuration.GetConnectionString(ConnectionStringName) ?? DefaultConnectionString);
        var dataSource = builder.DataSource;
        var isFile = dataSource.Length > 0 && dataSource != ":memory:" && !dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
        if (isFile && !Path.IsPathRooted(dataSource))
        {
            builder.DataSource = Path.Combine(environment.ContentRootPath, dataSource);
        }

        return builder.ToString();
    }
}
