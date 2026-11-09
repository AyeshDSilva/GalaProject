using Microsoft.AspNetCore.Mvc.Razor;

namespace GalaReconciler.Web.Shared.Infrastructure;

/// <summary>
/// Resolves views from the vertical slice folders instead of the default Views/ folder:
/// <c>Features/{Controller}/{View}.cshtml</c> first, then <c>Shared/Views/{View}.cshtml</c>.
/// </summary>
public sealed class FeatureViewLocationExpander : IViewLocationExpander
{
    private static readonly string[] Locations =
    [
        "/Features/{1}/{0}.cshtml",
        "/Shared/Views/{0}.cshtml",
    ];

    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations) =>
        Locations;
}
