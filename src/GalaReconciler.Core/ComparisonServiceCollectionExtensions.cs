using GalaReconciler.Core.Matching;
using GalaReconciler.Core.Parsing;
using GalaReconciler.Core.Rules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GalaReconciler.Core;

/// <summary>Registers the comparison pipeline. Both the web app and the console app use this.</summary>
public static class ComparisonServiceCollectionExtensions
{
    public static IServiceCollection AddComparison(this IServiceCollection services)
    {
        services.AddOptions<ComparisonOptions>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<SourceRsvpParser>();
        services.AddSingleton<ProcessedGuestListParser>();
        services.AddSingleton<GuestMatcher>();

        // Rules run in this order for every matched pair. Register new rules here.
        services.AddSingleton<IDiscrepancyRule, DatesMismatchRule>();
        services.AddSingleton<IDiscrepancyRule, MobileNumberMismatchRule>();
        services.AddSingleton<IDiscrepancyRule, EmailMismatchRule>();

        services.AddScoped<CompareFilesHandler>();
        return services;
    }
}
