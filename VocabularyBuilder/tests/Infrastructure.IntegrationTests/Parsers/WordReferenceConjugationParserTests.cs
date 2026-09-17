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
    public void ShouldNotRepeatAFormThatRecursAcrossTenses()
    {
        // "prends" is both 1st and 2nd person singular present
        var duplicates = _forms
            .GroupBy(f => f.Form)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        duplicates.Should().BeEmpty();
    }

    [Test]
    public void ShouldFindAPlausibleNumberOfForms()
    {
        // A French verb has dozens of distinct forms across all moods
        _forms.Count.Should().BeGreaterThan(30);
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
