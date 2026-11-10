using GalaReconciler.Core.Normalization;

namespace GalaReconciler.Tests.Normalization;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("0491 570 159", "0491570159")]
    [InlineData("0491570159", "0491570159")]
    [InlineData("491570159", "0491570159")]
    [InlineData("+61 491 570 159", "0491570159")]
    [InlineData("61491570159", "0491570159")]
    [InlineData("+61 (0) 491 570 159", "0491570159")]
    [InlineData("0061 491 570 159", "0491570159")]
    [InlineData("(08) 7010 0175", "0870100175")]
    [InlineData("- 0491570737", "0491570737")]
    [InlineData("049157156", "049157156")]
    public void Normalizes_a_single_number(string input, string expected) =>
        Assert.Equal(expected, PhoneNormalizer.Normalize(input));

    [Fact]
    public void Spaced_and_unspaced_numbers_are_equal() =>
        Assert.Equal(PhoneNormalizer.Normalize("0491 570 159"), PhoneNormalizer.Normalize("0491570159"));

    [Fact]
    public void Splits_a_multi_line_cell() =>
        Assert.Equal(["0491570737", "0870100175"], PhoneNormalizer.NormalizeAll("- 0491570737\n08 7010 0175"));

    [Theory]
    [InlineData("0491 572 549 / 0491 572 665")]
    [InlineData("0491 572 549, 0491 572 665")]
    [InlineData("0491 572 549; 0491 572 665")]
    [InlineData("0491 572 549 | 0491 572 665")]
    [InlineData("0491 572 549 or 0491 572 665")]
    [InlineData("0491 572 549\r\n0491 572 665")]
    public void Splits_on_every_separator(string cell) =>
        Assert.Equal(["0491572549", "0491572665"], PhoneNormalizer.NormalizeAll(cell));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("-")]
    [InlineData("n/a")]
    public void Ignores_empty_results(string? cell) => Assert.Empty(PhoneNormalizer.NormalizeAll(cell));

    [Fact]
    public void Removes_duplicates_within_a_cell() =>
        Assert.Equal(["0491570159"], PhoneNormalizer.NormalizeAll("0491 570 159\n491570159"));
}

public class NameNormalizerTests
{
    [Theory]
    [InlineData("Grant Taylor", "Grant-Taylor")]
    [InlineData("O’Connell", "O'Connell")]
    [InlineData("O‘Connell", "OConnell")]
    [InlineData("José", "Jose")]
    [InlineData("  Marco   BELLINI ", "marco bellini")]
    [InlineData("Dr. Jane Smith", "Jane Smith")]
    [InlineData("Prof Sir Jane Smith", "jane smith")]
    public void Treats_variants_as_equal(string a, string b) =>
        Assert.Equal(NameNormalizer.Normalize(a), NameNormalizer.Normalize(b));

    [Theory]
    [InlineData("Mr Mr Oscar Reid", "oscar reid")]
    [InlineData("Mrs Clara Walsh", "clara walsh")]
    [InlineData("Miss. Ana-María López", "ana maria lopez")]
    [InlineData("Dame", "dame")]
    public void Normalizes(string input, string expected) => Assert.Equal(expected, NameNormalizer.Normalize(input));

    [Fact]
    public void Key_joins_first_and_last() =>
        Assert.Equal("olivia grant taylor", NameNormalizer.Key("Olivia", "Grant-Taylor"));

    [Fact]
    public void StripTitles_keeps_casing() =>
        Assert.Equal("Oscar Reid", NameNormalizer.StripTitles("Mr Mr Oscar Reid"));
}

public class EmailNormalizerTests
{
    [Theory]
    [InlineData("nil")]
    [InlineData("NIL")]
    [InlineData("none")]
    [InlineData("-")]
    [InlineData("n/a")]
    [InlineData("")]
    public void Ignores_placeholders(string cell) => Assert.Empty(EmailNormalizer.NormalizeAll(cell));

    [Fact]
    public void Lowercases_and_trims() =>
        Assert.Equal(["ethan.cole@example.net"], EmailNormalizer.NormalizeAll("  Ethan.Cole@Example.NET "));

    [Fact]
    public void Splits_on_newlines_commas_semicolons_and_spaces() =>
        Assert.Equal(
            ["a@x.com", "b@x.com", "c@x.com", "d@x.com"],
            EmailNormalizer.NormalizeAll("a@x.com\nb@x.com, c@x.com; d@x.com nil"));
}

public class RequirementNormalizerTests
{
    [Theory]
    [InlineData("Vegetarian", new[] { "Vegetarian" })]
    [InlineData("Gluten free, no dairy", new[] { "Gluten free", "no dairy" })]
    [InlineData("Vegan; nut allergy\nno shellfish", new[] { "Vegan", "nut allergy", "no shellfish" })]
    [InlineData("Halal / Kosher", new[] { "Halal / Kosher" })]
    [InlineData("Other", new string[0])]
    [InlineData("Other (please specify)", new string[0])]
    [InlineData("Yes", new string[0])]
    [InlineData("None", new string[0])]
    [InlineData("nil", new string[0])]
    [InlineData("N/A", new string[0])]
    [InlineData("No dietary requirements", new string[0])]
    [InlineData("-", new string[0])]
    [InlineData("", new string[0])]
    public void Splits_answers_and_drops_ones_that_are_not_requirements(string cell, string[] expected) =>
        Assert.Equal(expected, RequirementNormalizer.Parts(cell));

    [Theory]
    [InlineData("Gluten free", "Gluten-free", true)]
    [InlineData("gluten free", "Vegetarian, GLUTEN FREE.", true)]
    [InlineData("Wheelchair access", "Wheelchair access, aisle seat", true)]
    [InlineData("No dairy", "Gluten-free", false)]
    [InlineData("Coeliac", "Gluten free", false)]
    [InlineData("Vegan", null, false)]
    public void A_requirement_is_covered_when_the_master_list_mentions_it(string part, string? processed, bool covered) =>
        Assert.Equal(covered, RequirementNormalizer.IsCoveredBy(part, processed));

    [Fact]
    public void Adds_each_requirement_once()
    {
        var parts = new List<string> { "Gluten free" };
        RequirementNormalizer.AddDistinct(parts, ["gluten-free", "No dairy", "no dairy"]);
        Assert.Equal(["Gluten free", "No dairy"], parts);
    }
}
