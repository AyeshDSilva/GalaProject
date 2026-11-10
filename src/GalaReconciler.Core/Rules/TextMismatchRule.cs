using GalaReconciler.Core.Domain;
using GalaReconciler.Core.Matching;
using GalaReconciler.Core.Normalization;
using GalaReconciler.Core.Parsing;

namespace GalaReconciler.Core.Rules;

/// <summary>
/// Shared logic for single free-text fields. A blank or "nil" value in the RSVP export never raises anything
/// (the master list is left alone); otherwise the two values must match, ignoring case, full stops and spacing.
/// </summary>
public abstract class TextMismatchRule : IDiscrepancyRule
{
    public abstract DiscrepancyType Type { get; }

    protected abstract string? SelectValue(GuestRecord record);

    /// <summary>Lets a rule skip pairs where the field means something else.</summary>
    protected virtual bool ShouldCompare(MatchedPair pair) => true;

    /// <summary>Values with the same key count as the same.</summary>
    protected virtual string ComparisonKey(string value) => TextNormalizer.ComparisonKey(value);

    public Discrepancy? Evaluate(MatchedPair pair)
    {
        var source = TextNormalizer.Clean(SelectValue(pair.Source));
        if (source is null || !ShouldCompare(pair))
        {
            return null;
        }

        var processed = TextNormalizer.Clean(SelectValue(pair.Processed));
        if (processed is not null && ComparisonKey(source) == ComparisonKey(processed))
        {
            return null;
        }

        var detail = processed is null
            ? "Missing in Processed"
            : $"Source has “{source}”, Processed has “{processed}”";
        return DiscrepancyFactory.ForPair(pair, Type, detail) with
        {
            SourceValue = source,
            ProcessedValue = processed,
        };
    }
}

/// <summary>Title: in the export, "Please specify your title" wins over Title; compared with Prenom.</summary>
public sealed class TitleMismatchRule : TextMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.TitleMismatch;

    protected override string? SelectValue(GuestRecord record) => record.Title;

    /// <summary>"Miss" and "Ms" are used interchangeably, so they count as the same title.</summary>
    protected override string ComparisonKey(string value)
    {
        var key = base.ComparisonKey(value);
        return key == "miss" ? "ms" : key;
    }
}

/// <summary>Post nominal: "Please specify your post nominal" wins over Post nominal; compared with Postnom.</summary>
public sealed class PostNominalMismatchRule : TextMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.PostNominalMismatch;

    protected override string? SelectValue(GuestRecord record) => record.PostNominal;
}

/// <summary>Position title, compared with Position. Master-list rows that say "Guest of …" are skipped.</summary>
public sealed class PositionMismatchRule : TextMismatchRule
{
    public override DiscrepancyType Type => DiscrepancyType.PositionMismatch;

    protected override string? SelectValue(GuestRecord record) => record.Position;

    protected override bool ShouldCompare(MatchedPair pair) => !ProcessedGuestListParser.IsHostLink(pair.Processed.Position);
}
