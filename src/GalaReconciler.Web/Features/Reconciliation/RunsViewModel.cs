using GalaReconciler.Core.Domain;
using GalaReconciler.Web.Features.Reconciliation.Persistence;

namespace GalaReconciler.Web.Features.Reconciliation;

/// <summary>Filters for the runs list, bound from the query string.</summary>
public sealed class RunFilter
{
    /// <summary>Part of either file name.</summary>
    public string? File { get; set; }

    /// <summary>Part of a guest or host name that appears in the run's differences.</summary>
    public string? Guest { get; set; }

    public RunStatus? Status { get; set; }

    /// <summary>Only runs with at least one difference of this type.</summary>
    public DiscrepancyType? Type { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public int Page { get; set; } = 1;

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(File) && string.IsNullOrWhiteSpace(Guest) && Status is null && Type is null && From is null && To is null;

    /// <summary>Query string values for a link to another page of the same filtered list.</summary>
    public Dictionary<string, string> ToRouteValues(int page)
    {
        var values = new Dictionary<string, string>();
        void Add(string key, object? value)
        {
            if (value is not null && value.ToString() is { Length: > 0 } text)
            {
                values[key] = text;
            }
        }

        Add(nameof(File), File);
        Add(nameof(Guest), Guest);
        Add(nameof(Status), Status);
        Add(nameof(Type), Type);
        Add(nameof(From), From?.ToString("yyyy-MM-dd"));
        Add(nameof(To), To?.ToString("yyyy-MM-dd"));
        if (page > 1)
        {
            values[nameof(Page)] = page.ToString();
        }

        return values;
    }
}

public sealed record RunsViewModel(RunFilter Filter, IReadOnlyList<Run> Runs, int TotalCount, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public int Page => Math.Clamp(Filter.Page, 1, TotalPages);

    public static string LocalTime(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("d MMM yyyy, h:mm tt");
}
