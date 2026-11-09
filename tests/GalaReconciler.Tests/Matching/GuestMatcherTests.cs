using GalaReconciler.Web.Features.Reconciliation.Matching;
using static GalaReconciler.Tests.Helpers.TestGuests;

namespace GalaReconciler.Tests.Matching;

public class GuestMatcherTests
{
    private readonly GuestMatcher _matcher = new();

    [Fact]
    public void Matches_on_normalized_name()
    {
        var result = _matcher.Match([Guest("Olivia Grant Taylor")], [Guest("Olivia Grant-Taylor")]);

        var pair = Assert.Single(result.Matched);
        Assert.Equal(MatchMethod.Name, pair.MatchedBy);
        Assert.Empty(result.OnlyInSource);
        Assert.Empty(result.OnlyInProcessed);
    }

    [Fact]
    public void Falls_back_to_a_shared_email()
    {
        var result = _matcher.Match(
            [Guest("Bill Hartley").WithEmails("bill@x.com")],
            [Guest("William Hartley").WithEmails("bill@x.com", "w@y.com")]);

        var pair = Assert.Single(result.Matched);
        Assert.Equal(MatchMethod.Email, pair.MatchedBy);
        Assert.Equal("William Hartley", pair.DisplayName);
        Assert.Equal("Bill Hartley", pair.SourceDisplayName);
    }

    [Fact]
    public void Does_not_match_on_email_when_ambiguous()
    {
        var result = _matcher.Match(
            [Guest("Bill Hartley").WithEmails("family@x.com")],
            [Guest("William Hartley").WithEmails("family@x.com"), Guest("Wendy Hartley").WithEmails("family@x.com")]);

        Assert.Empty(result.Matched);
    }

    [Fact]
    public void Never_matches_on_phone()
    {
        var result = _matcher.Match(
            [Guest("Henry Walsh").WithPhones("0491571266")],
            [Guest("Clara Walsh").WithPhones("0491571266")]);

        Assert.Empty(result.Matched);
        Assert.Single(result.OnlyInSource);
        Assert.Single(result.OnlyInProcessed);
    }

    [Fact]
    public void Suggests_a_close_name_but_does_not_match_it()
    {
        var result = _matcher.Match([Guest("Ruby Macintyre"), Guest("Sam Fletcher")], [Guest("Ruby McIntyre")]);

        Assert.Empty(result.Matched);
        Assert.Equal("Ruby McIntyre", result.OnlyInSource.Single(u => u.Record.DisplayName == "Ruby Macintyre").PossibleMatch);
        Assert.Null(result.OnlyInSource.Single(u => u.Record.DisplayName == "Sam Fletcher").PossibleMatch);
        Assert.Equal("Ruby Macintyre", Assert.Single(result.OnlyInProcessed).PossibleMatch);
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("ruby macintyre", "ruby mcintyre", 1)]
    [InlineData("", "abc", 3)]
    [InlineData("same", "same", 0)]
    public void Levenshtein_distance(string a, string b, int expected) => Assert.Equal(expected, Levenshtein.Distance(a, b));
}
