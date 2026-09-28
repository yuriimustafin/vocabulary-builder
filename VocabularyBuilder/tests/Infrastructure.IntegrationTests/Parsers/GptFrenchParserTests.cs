using FluentAssertions;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.HttpClients;
using VocabularyBuilder.Infrastructure.Parsers;

namespace VocabularyBuilder.Infrastructure.IntegrationTests.Parsers;

/// <summary>
/// The GPT parser against the recorded responses, which is the path production actually takes:
/// WordReference blocks the deployed host, so GPT is the default source for French.
/// </summary>
public class GptFrenchParserTests
{
    private static GptFrenchParser CreateParser() => new(new MockGptClient(FixturePath()));

    /// <summary>
    /// The recorded responses ship with the Web project, which is where the running app reads
    /// them from; the tests read the same files rather than a copy that could drift.
    /// </summary>
    internal static string FixturePath()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Web")))
        {
            directory = directory.Parent;
        }

        Directory.Exists(Path.Combine(directory?.FullName ?? "", "src", "Web"))
            .Should().BeTrue("the tests have to be able to find src/Web to read MockData/gpt");

        return Path.Combine(directory!.FullName, "src", "Web", "MockData", "gpt");
    }

    private static async Task<Domain.Samples.Entities.Word> Parse(string headword)
    {
        var results = await CreateParser().GetWordsWithSource(new[] { headword });

        return results.Should().ContainSingle().Which.Word;
    }

    [Test]
    public async Task ShouldReadTheGenderThatAnArticleIsDerivedFrom()
    {
        var word = await Parse("maison");

        word.Gender.Should().Be(GrammaticalGender.Feminine);
        word.PartOfSpeech.Should().Be("noun");
        word.Transcription.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// The gap the WordReference block left: a French sense indicator, which is what the
    /// bilingual line on a card is drawn from. Without it every production card showed the
    /// English meaning alone.
    /// </summary>
    [Test]
    public async Task ShouldReadTheFrenchGlossBesideTheMeaningItBelongsTo()
    {
        var word = await Parse("maison");

        var sense = word.Senses.Should().HaveCountGreaterThan(1).And.Subject.First();

        sense.Definition.Should().Be("house, home");
        sense.Gloss.Should().Be("bâtiment d'habitation");
    }

    /// <summary>
    /// A gloss names a sense rather than giving it, so it must not simply repeat the English.
    /// </summary>
    [Test]
    public async Task ShouldNotRepeatTheMeaningInTheGloss()
    {
        var word = await Parse("comprendre");

        foreach (var sense in word.Senses!.Where(s => s.Gloss is not null))
        {
            sense.Gloss.Should().NotBe(sense.Definition);
        }
    }

    /// <summary>
    /// Nothing to tell apart means no gloss - an empty one would draw a blank line on the card.
    /// </summary>
    [Test]
    public async Task ShouldLeaveTheGlossOffAWordWithOneSense()
    {
        var word = await Parse("merci");

        word.Senses.Should().ContainSingle().Which.Gloss.Should().BeNull();
    }

    [Test]
    public async Task ShouldKeepExamplesApartFromTheirTranslations()
    {
        var word = await Parse("maison");

        var sense = word.Senses!.First();

        sense.Examples.Should().NotBeEmpty();
        sense.Examples[0].Should().Contain("maison");
        sense.ExampleTranslations.Should().NotBeNull();
        sense.ExampleTranslations![0].Should().NotContain("maison");
    }

    [Test]
    public async Task ShouldReportItselfAsTheGptSource()
    {
        CreateParser().SourceType.Should().Be(DictionarySourceType.Gpt);
    }

    /// <summary>
    /// A model that answers "(saisir)" or the string "null" is answering the prompt loosely
    /// rather than wrongly, and both have been seen.
    /// </summary>
    [TestCase("(saisir)", "saisir")]
    [TestCase("  saisir  ", "saisir")]
    [TestCase("null", null)]
    [TestCase("", null)]
    [TestCase("   ", null)]
    public async Task ShouldNormaliseWhateverShapeTheGlossArrivesIn(string given, string? expected)
    {
        var client = new StubGptClient($@"{{
          ""lemma"": ""prendre"",
          ""partOfSpeech"": ""verb"",
          ""senses"": [ {{ ""definition"": ""to take"", ""gloss"": ""{given}"", ""examples"": [] }} ]
        }}");

        var results = await new GptFrenchParser(client).GetWordsWithSource(new[] { "prendre" });

        results.Single().Word.Senses!.Single().Gloss.Should().Be(expected);
    }

    private sealed class StubGptClient : Application.Ai.IGptClient
    {
        private readonly string _response;

        public StubGptClient(string response) => _response = response;

        public Task<string?> SendMessageAsync(string prompt) => Task.FromResult<string?>(_response);
    }
}
