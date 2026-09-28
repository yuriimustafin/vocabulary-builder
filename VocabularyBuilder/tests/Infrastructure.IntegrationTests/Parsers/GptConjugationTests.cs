using FluentAssertions;
using Microsoft.Extensions.Options;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.HttpClients;
using VocabularyBuilder.Infrastructure.Parsers;

namespace VocabularyBuilder.Infrastructure.IntegrationTests.Parsers;

/// <summary>
/// Conjugations from the model, which is the only source the deployed host can reach:
/// WordReference's conjugation page is a second request to a site that answers it with 418.
/// </summary>
public class GptConjugationTests
{
    private static GptFrenchParser CreateParser(bool includeConjugations = true) =>
        new(new MockGptClient(GptFrenchParserTests.FixturePath()),
            Options.Create(new GptDictionaryOptions { IncludeConjugations = includeConjugations }));

    private static async Task<Application.Parsers.WordParseResult> Parse(
        string headword, bool includeConjugations = true)
    {
        var results = await CreateParser(includeConjugations).GetWordsWithSource(new[] { headword });

        return results.Should().ContainSingle().Which;
    }

    [Test]
    public async Task ShouldReadEveryMoodAConjugationTablePrints()
    {
        var result = await Parse("prendre");

        result.Forms.Select(f => f.Mood).Distinct().Should().BeEquivalentTo(new[]
        {
            "participe", "indicatif", "formes composées", "subjonctif", "conditionnel", "impératif"
        });
    }

    /// <summary>
    /// The participles are what the word list shows for a verb, and they carry no subject.
    /// </summary>
    [Test]
    public async Task ShouldReadTheParticiplesWithNoPerson()
    {
        var result = await Parse("prendre");

        var participles = result.Forms.Where(f => f.Mood == "participe").ToList();

        participles.Select(f => f.Form).Should().Contain(new[] { "prenant", "pris" });
        participles.Should().OnlyContain(f => f.Person == null);
    }

    [Test]
    public async Task ShouldKeepOneRowPerCellSoARecurringFormIsNotLost()
    {
        var result = await Parse("prendre");

        var present = result.Forms
            .Where(f => f.Mood == "indicatif" && f.Tense == "présent")
            .ToList();

        present.Should().HaveCount(6, "one per person, as the table prints it");
        present.Select(f => f.Person).Should().Equal(
            "je", "tu", "il, elle, on", "nous", "vous", "ils, elles");

        // "prends" is both first and second person, and both rows have to survive
        present.Where(f => f.Form == "prends").Should().HaveCount(2);
    }

    [Test]
    public async Task ShouldRecordEveryFormAgainstFrench()
    {
        var result = await Parse("prendre");

        result.Forms.Should().OnlyContain(f => f.Language == Language.French);
    }

    /// <summary>
    /// The table is a second document for the same word, so it is cached on its own.
    /// </summary>
    [Test]
    public async Task ShouldCacheTheTableUnderItsOwnUrl()
    {
        var result = await Parse("prendre");

        result.ConjugationUrl.Should().Be("gpt://french/conjugation/prendre");
        result.ConjugationHtml.Should().NotBeNullOrWhiteSpace();
        result.SourceUrl.Should().NotBe(result.ConjugationUrl);
    }

    [Test]
    public async Task ShouldAskForNothingWhenConjugationsAreTurnedOff()
    {
        var result = await Parse("prendre", includeConjugations: false);

        result.Forms.Should().BeEmpty();
        result.ConjugationHtml.Should().BeNull();
    }

    /// <summary>
    /// Only verbs cost a second call.
    /// </summary>
    [Test]
    public async Task ShouldNotAskForTheConjugationOfANoun()
    {
        var result = await Parse("maison");

        result.Forms.Should().BeEmpty();
        result.ConjugationHtml.Should().BeNull();
    }

    /// <summary>
    /// A verb the model has nothing to say about keeps its entry; only the table is lost. The
    /// mock has no recording for this one, and answers nothing rather than inventing a table.
    /// </summary>
    [Test]
    public async Task ShouldKeepTheEntryWhenNoConjugationComesBack()
    {
        var result = await Parse("parler");

        result.Word.Headword.Should().Be("parler");
        result.Word.Senses.Should().NotBeNullOrEmpty();
        result.Forms.Should().BeEmpty();
        result.ConjugationHtml.Should().BeNull();
    }

    /// <summary>
    /// A model wrapping its JSON in prose or a code fence is common enough to handle, and the
    /// reader is shared with the dictionary response for that reason.
    /// </summary>
    [Test]
    public void ShouldReadATableOutOfAFencedResponse()
    {
        var response = "Here you go:\n```json\n"
            + "{\"moods\":[{\"mood\":\"indicatif\",\"tenses\":[{\"tense\":\"présent\","
            + "\"forms\":[{\"person\":\"je\",\"form\":\"vais\"}]}]}]}\n```\nHope that helps.";

        var forms = GptFrenchParser.ReadConjugation(response);

        forms.Should().ContainSingle();
        forms[0].Form.Should().Be("vais");
        forms[0].Mood.Should().Be("indicatif");
        forms[0].Tense.Should().Be("présent");
        forms[0].Person.Should().Be("je");
    }

    [TestCase("not json at all")]
    [TestCase("{}")]
    [TestCase("{\"moods\":[]}")]
    [TestCase("{\"moods\":[{\"mood\":\"indicatif\",\"tenses\":[]}]}")]
    public void ShouldReadNoFormsRatherThanThrowOnAnAnswerItCannotUse(string response)
    {
        GptFrenchParser.ReadConjugation(response).Should().BeEmpty();
    }

    /// <summary>
    /// A blank form is a hole in the table, not a form.
    /// </summary>
    [Test]
    public void ShouldSkipABlankForm()
    {
        var response = "{\"moods\":[{\"mood\":\"indicatif\",\"tenses\":[{\"tense\":\"présent\","
            + "\"forms\":[{\"person\":\"je\",\"form\":\"  \"},{\"person\":\"tu\",\"form\":\"vas\"}]}]}]}";

        var forms = GptFrenchParser.ReadConjugation(response);

        forms.Should().ContainSingle().Which.Form.Should().Be("vas");
    }
}
