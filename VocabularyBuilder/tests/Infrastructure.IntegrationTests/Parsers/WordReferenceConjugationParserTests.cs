using FluentAssertions;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Parsers;

namespace VocabularyBuilder.Infrastructure.IntegrationTests.Parsers;

/// <summary>
/// Parses a page recorded from
/// wordreference.com/conj/frverbs.aspx?v=prendre.
/// </summary>
public class WordReferenceConjugationParserTests
{
    private IReadOnlyList<WordForm> _forms = null!;

    [OneTimeSetUp]
    public async Task ParseFixture()
    {
        var html = await File.ReadAllTextAsync(
            WordReferenceFrenchParserTests.FixturePath("prendre.conj.html"));

        _forms = await new WordReferenceConjugationParser().GetFormsAsync(html);
    }

    [Test]
    public void ShouldReadTheSimpleConjugatedForms()
    {
        var forms = _forms.Select(f => f.Form).ToList();

        // Present indicative, across the stem changes prend-/pren-/prenn-
        forms.Should().Contain("prends");
        forms.Should().Contain("prend");
        forms.Should().Contain("prenons");
        forms.Should().Contain("prenez");
        forms.Should().Contain("prennent");
    }

    [Test]
    public void ShouldReadFormsWhoseStemIsSplitAcrossBoldTags()
    {
        // "<b>pr</b>is" and "<b>pr</b>îmes" must come back whole
        var forms = _forms.Select(f => f.Form).ToList();

        forms.Should().Contain("pris");
        forms.Should().Contain("prîmes");
        forms.Should().Contain("prirent");
    }

    [Test]
    public void ShouldReadFutureAndImperfectForms()
    {
        var forms = _forms.Select(f => f.Form).ToList();

        forms.Should().Contain("prendrai");
        forms.Should().Contain("prendront");
        forms.Should().Contain("prenais");
        forms.Should().Contain("prenaient");
    }

    [Test]
    public void ShouldReadCompoundTensesAsWholeForms()
    {
        // "j' | ai <b>pr</b><b>is</b>" is one form, not two
        var forms = _forms.Select(f => f.Form).ToList();

        forms.Should().Contain("ai pris");
    }

    [Test]
    public void ShouldRecordTheMoodAndTenseOfEachForm()
    {
        var present = _forms.First(f => f.Form == "prenons");

        present.Mood.Should().Be("indicatif");
        present.Tense.Should().Be("présent");
        present.Person.Should().Be("nous");
    }

    [Test]
    public void ShouldTagEveryFormAsFrench()
    {
        _forms.Should().OnlyContain(f => f.Language == Language.French);
    }

    [Test]
    public void ShouldKeepEveryCellEvenWhenAFormRecurs()
    {
        // "prends" is both 1st and 2nd person singular present; dropping the second would
        // leave a hole where "tu" belongs when the table is shown
        var present = _forms
            .Where(f => f.Mood == "indicatif" && f.Tense == "présent")
            .Select(f => (f.Person, f.Form))
            .ToList();

        present.Should().Equal(
            ("je", "prends"),
            ("tu", "prends"),
            ("il, elle, on", "prend"),
            ("nous", "prenons"),
            ("vous", "prenez"),
            ("ils, elles", "prennent"));
    }

    [Test]
    public void ShouldReadEveryTableOnThePage()
    {
        // 15 six-person tables and 2 imperatives with three persons each, plus 2 participles
        _forms.Count.Should().Be(15 * 6 + 2 * 3 + 2);
    }

    [Test]
    public void ShouldReadTheParticiples()
    {
        _forms.Should().ContainSingle(f => f.Mood == "participe" && f.Tense == "présent")
            .Which.Form.Should().Be("prenant");

        _forms.Should().ContainSingle(f => f.Mood == "participe" && f.Tense == "passé")
            .Which.Form.Should().Be("pris");
    }

    [Test]
    public void ShouldNotKeepTheInfinitiveOrThePronominalVerbAsForms()
    {
        _forms.Should().NotContain(f => f.Form == "prendre" || f.Form.StartsWith("se prendre"));
    }

    [Test]
    public void ShouldCleanTheImperative()
    {
        var imperative = _forms.Where(f => f.Mood == "impératif" && f.Tense == "présent").ToList();

        // The persons the imperative lacks are marked with a dash on the page
        imperative.Should().NotContain(f => f.Form == "–" || f.Form == "-");
        // "prends !" is the form "prends"; "(tu)" is the person "tu"
        imperative.Select(f => (f.Person, f.Form)).Should().Equal(
            ("tu", "prends"),
            ("nous", "prenons"),
            ("vous", "prenez"));
    }

    [Test]
    public async Task ShouldReturnNothingForHtmlWithoutConjugationTables()
    {
        var forms = await new WordReferenceConjugationParser()
            .GetFormsAsync("<html><body>not a conjugation page</body></html>");

        forms.Should().BeEmpty();
    }

    [Test]
    public async Task ShouldReturnNothingForEmptyHtml()
    {
        var forms = await new WordReferenceConjugationParser().GetFormsAsync(string.Empty);

        forms.Should().BeEmpty();
    }
}
