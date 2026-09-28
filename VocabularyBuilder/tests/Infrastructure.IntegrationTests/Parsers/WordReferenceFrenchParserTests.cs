using FluentAssertions;
using Microsoft.Extensions.Options;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.Parsers;

namespace VocabularyBuilder.Infrastructure.IntegrationTests.Parsers;

/// <summary>
/// Parses a page recorded from wordreference.com/fren/prendre, so the tests
/// exercise the real markup without depending on the network.
/// </summary>
public class WordReferenceFrenchParserTests
{
    private string _html = null!;

    [OneTimeSetUp]
    public void LoadFixture()
    {
        _html = File.ReadAllText(FixturePath("prendre.html"));
    }

    internal static string FixturePath(string fileName)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", fileName);
    }

    private static WordReferenceFrenchParser CreateParser(int maxTranslations = 5)
    {
        var options = Options.Create(new WordReferenceOptions
        {
            MaxTranslations = maxTranslations,
            RequestDelayMilliseconds = 0
        });

        return new WordReferenceFrenchParser(
            options,
            new MockWordReferencePageLoader(FixturePath("")),
            new WordReferenceConjugationParser());
    }

    [Test]
    public async Task ShouldParseHeadwordAndPronunciation()
    {
        var word = await CreateParser().GetWordFromCachedHtml(_html);

        word.Should().NotBeNull();
        word!.Headword.Should().Be("prendre");
        word.Language.Should().Be(Language.French);
        // <span id='pronWR'>[pʀɑ̃dʀ]</span>, with the brackets stripped
        word.Transcription.Should().Be("pʀɑ̃dʀ");
    }

    [Test]
    public async Task ShouldLimitSensesToTheConfiguredMaximum()
    {
        // The Principal Translations section lists 15 entries for "prendre"
        var word = await CreateParser(maxTranslations: 5).GetWordFromCachedHtml(_html);

        word!.Senses.Should().HaveCount(5);
    }

    [Test]
    public async Task ShouldRespectAHigherConfiguredMaximum()
    {
        var word = await CreateParser(maxTranslations: 12).GetWordFromCachedHtml(_html);

        word!.Senses.Should().HaveCount(12);
    }

    /// <summary>
    /// The gloss says which sense is meant and the translations say what it means. They are
    /// kept apart so that whatever shows them decides how, and so that anything comparing one
    /// meaning against another gets the meaning and nothing wrapped around it.
    /// </summary>
    [Test]
    public async Task ShouldKeepTheFrenchGlossApartFromItsEnglishTranslations()
    {
        var word = await CreateParser().GetWordFromCachedHtml(_html);

        var first = word!.Senses!.First();

        first.Gloss.Should().Be("saisir");
        first.Definition.Should().Contain("take");
        first.Definition.Should().Contain("grasp");
        first.Definition.Should().NotContain("saisir");
        first.Definition.Should().NotStartWith("(");
        first.PartOfSpeech.Should().Be(PartsOfSpeech.Verb);
    }

    /// <summary>
    /// An example is stored as it was written, with its translation beside it by position -
    /// not appended to it in brackets, which a cloze exercise would then have to blank around.
    /// </summary>
    [Test]
    public async Task ShouldKeepAnExampleApartFromItsTranslation()
    {
        var word = await CreateParser().GetWordFromCachedHtml(_html);

        var sense = word!.Senses!.First(s => s.Examples.Any());

        sense.Examples.Should().NotBeEmpty();
        sense.Examples.Should().OnlyContain(e => !e.Contains('('));
        sense.ExampleTranslations.Should().NotBeNull();
        sense.ExampleTranslations!.Count.Should().Be(sense.Examples.Count);
    }

    [Test]
    public async Task ShouldStripPartOfSpeechTagsAndConjugationArrowsFromTranslations()
    {
        var word = await CreateParser().GetWordFromCachedHtml(_html);

        var definitions = string.Join(" | ", word!.Senses!.Select(s => s.Definition));

        // "vtr", "vtr phrasal sep" and the conjugation arrow are markup, not meaning
        definitions.Should().NotContain("⇒");
        definitions.Should().NotContain("vtr");
        definitions.Should().NotContain("phrasal");
    }

    [Test]
    public async Task ShouldPairFrenchExamplesWithTheirEnglishTranslation()
    {
        var word = await CreateParser().GetWordFromCachedHtml(_html);

        // Paired by position rather than by being written into one string
        var pairs = word!.Senses!
            .SelectMany(sense => sense.Examples.Select((example, index) => new
            {
                French = example,
                English = sense.ExampleTranslations is not null && index < sense.ExampleTranslations.Count
                    ? sense.ExampleTranslations[index]
                    : null
            }))
            .ToList();

        pairs.Should().NotBeEmpty();
        pairs.Should().Contain(p =>
            p.French == "N'oublie pas de prendre tes papiers."
            && p.English == "Don't forget to take your papers.");
    }

    [Test]
    public async Task ShouldReadThePartOfSpeechFromTheEncodedAbbreviation()
    {
        var word = await CreateParser().GetWordFromCachedHtml(_html);

        // data-abbr='vtr' on the French side of the first entry
        word!.PartOfSpeech.Should().Be("vtr");
    }

    [Test]
    public async Task ShouldIgnoreCompoundFormsSection()
    {
        // "prendre" has 100 compound forms (prendre garde, prendre feu...);
        // none of them may leak into the senses
        var word = await CreateParser(maxTranslations: 50).GetWordFromCachedHtml(_html);

        var definitions = string.Join(" | ", word!.Senses!.Select(s => s.Definition));
        definitions.Should().NotContain("prendre garde");

        // Principal Translations holds 15 entries, so the cap is never reached
        word.Senses!.Count.Should().Be(15);
    }

    [Test]
    public async Task ShouldFindTheConjugationUrlForAVerb()
    {
        var url = await CreateParser().GetConjugationUrlFromHtml(_html);

        url.Should().Be("https://www.wordreference.com/conj/frverbs.aspx?v=prendre");
    }

    [Test]
    public async Task ShouldReturnNullForHtmlThatIsNotAWordReferenceEntry()
    {
        var word = await CreateParser().GetWordFromCachedHtml("<html><body>No entry found</body></html>");

        word.Should().BeNull();
    }

    [Test]
    public async Task ShouldReturnNullForEmptyHtml()
    {
        var word = await CreateParser().GetWordFromCachedHtml(string.Empty);

        word.Should().BeNull();
    }

    [Test]
    public async Task ShouldFetchTheEntryAndItsConjugationTogether()
    {
        // Exercises the whole fetch path against the recorded pages
        var results = (await CreateParser().GetWordsWithSource(new[] { "prendre" })).ToList();

        results.Should().HaveCount(1);

        var result = results[0];
        result.Word.Headword.Should().Be("prendre");
        result.SourceUrl.Should().Be("https://www.wordreference.com/fren/prendre");
        result.SourceHtml.Should().NotBeEmpty();

        // The conjugation page is followed from the entry's own link
        result.ConjugationUrl.Should().Be("https://www.wordreference.com/conj/frverbs.aspx?v=prendre");
        result.ConjugationHtml.Should().NotBeNullOrEmpty();
        result.Forms.Select(f => f.Form).Should().Contain(new[] { "prends", "prend", "prenons", "pris" });
    }

    [Test]
    public async Task ShouldSkipTheConjugationWhenTurnedOff()
    {
        var options = Options.Create(new WordReferenceOptions
        {
            MaxTranslations = 5,
            RequestDelayMilliseconds = 0,
            IncludeConjugations = false
        });

        var parser = new WordReferenceFrenchParser(
            options,
            new MockWordReferencePageLoader(FixturePath("")),
            new WordReferenceConjugationParser());

        var results = (await parser.GetWordsWithSource(new[] { "prendre" })).ToList();

        results.Should().HaveCount(1);
        results[0].Forms.Should().BeEmpty();
        results[0].ConjugationHtml.Should().BeNull();
    }

    [Test]
    public async Task ShouldReturnNothingForAWordWithNoRecordedPage()
    {
        var results = await CreateParser().GetWordsWithSource(new[] { "motinexistant" });

        results.Should().BeEmpty();
    }

    [Test]
    public void ShouldDeclareItselfAsTheWordReferenceSource()
    {
        CreateParser().SourceType.Should().Be(DictionarySourceType.WordReference);
    }
}
