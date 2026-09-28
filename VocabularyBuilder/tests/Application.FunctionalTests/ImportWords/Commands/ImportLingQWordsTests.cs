using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.ImportWords.Commands;

using static Testing;

/// <summary>
/// The LingQ import against a real database, which is where the behaviour that matters
/// lives: one word per headword, one encounter per form met.
/// </summary>
/// <remarks>
/// Every term used here is one the rules settle on their own, so these tests never reach
/// the analyzer and never make a request.
/// </remarks>
public class ImportLingQWordsTests : BaseTestFixture
{
    private const string Header =
        "term,phrase,tag1,tag2,tag3,tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2";

    private static ImportLingQWordsCommand Command(string rows, string? listName = null) =>
        new()
        {
            FileContent = Header + "\n" + rows,
            Language = Language.French,
            ListName = listName
        };

    [Test]
    public async Task ShouldStoreTheHeadwordWithoutItsArticle()
    {
        await SendAsync(Command("une conférence,,preply,,,,en,conference,,\n"));

        var words = await ListAsync<Word>();

        words.Should().ContainSingle();
        words[0].Headword.Should().Be("conférence");
        words[0].Language.Should().Be(Language.French);
    }

    /// <summary>
    /// The requirement this import exists for: two forms of one word are one word met
    /// twice, not two words.
    /// </summary>
    [Test]
    public async Task ShouldCountTwoFormsOfOneWordAsTwoEncounters()
    {
        await SendAsync(Command(
            "une randonnée,,preply,,,,en,a hike,,\n" +
            "la randonnée,,preply,,,,en,hiking,,\n"));

        var words = await ListAsync<Word>();
        var encounters = await ListAsync<WordEncounter>();

        words.Should().ContainSingle().Which.Headword.Should().Be("randonnée");
        encounters.Should().HaveCount(2);
        encounters.Should().OnlyContain(e => e.Source == WordEncounterSource.LingQ);
    }

    /// <summary>
    /// Re-importing an export must not inflate the counts, or every import would make
    /// every word look better known than it is.
    /// </summary>
    [Test]
    public async Task ShouldAddNothingWhenTheSameExportIsImportedAgain()
    {
        var command = Command("une main,,preply,,,,en,hand,,\nun pied,,preply,,,,en,foot,,\n");

        await SendAsync(command);
        var result = await SendAsync(command);

        result.WordsCreated.Should().Be(0);
        result.EncountersCreated.Should().Be(0);

        (await CountAsync<Word>()).Should().Be(2);
        (await CountAsync<WordEncounter>()).Should().Be(2);
    }

    /// <summary>
    /// A later export contains everything the earlier one did, so only its new rows
    /// should count.
    /// </summary>
    [Test]
    public async Task ShouldAddOnlyTheNewRowsOfAGrownExport()
    {
        const string listName = "Preply";

        await SendAsync(Command("une main,,preply,,,,en,hand,,\n", listName));

        var result = await SendAsync(Command(
            "une main,,preply,,,,en,hand,,\nun pied,,preply,,,,en,foot,,\n", listName));

        result.WordsCreated.Should().Be(1);
        result.EncountersCreated.Should().Be(1);

        (await CountAsync<Word>()).Should().Be(2);
        (await CountAsync<WordEncounter>()).Should().Be(2);
    }

    /// <summary>
    /// A word already known from elsewhere gains an encounter rather than a duplicate row.
    /// </summary>
    [Test]
    public async Task ShouldAddAnEncounterToAWordAlreadyInTheVocabulary()
    {
        await AddAsync(new Word { Headword = "main", Language = Language.French });

        var result = await SendAsync(Command("une main,,preply,,,,en,hand,,\n"));

        result.WordsCreated.Should().Be(0);
        result.EncountersCreated.Should().Be(1);

        (await CountAsync<Word>()).Should().Be(1);
    }

    /// <summary>
    /// Imported words are filled in from the dictionary later, so nothing arrives with
    /// the learner's own translation attached.
    /// </summary>
    [Test]
    public async Task ShouldNotTakeItsContentFromTheImportSource()
    {
        await SendAsync(Command("une conférence,J'écoute une conférence.,,,,,en,conference,,\n"));

        var word = (await ListAsync<Word>()).Single();
        var encounter = (await ListAsync<WordEncounter>()).Single();

        word.Senses.Should().BeNullOrEmpty();
        word.PartOfSpeech.Should().BeNull();
        encounter.Notes.Should().Be("J'écoute une conférence.", "the sentence is context, not content");
    }

    [Test]
    public async Task ShouldNotImportProse()
    {
        var result = await SendAsync(Command(
            "une main,,preply,,,,en,hand,,\n" +
            "Quel temps fait-il,,preply,,,,en,What's the weather like?,,\n" +
            "j'ai trente ans,,,,,,en,i'm thirty,,\n"));

        result.TermsRead.Should().Be(3);
        result.TermsImported.Should().Be(1);
        result.Skipped.Should().HaveCount(2);

        (await CountAsync<Word>()).Should().Be(1);
    }

    [Test]
    public async Task ShouldRecordTheEncounterAgainstTheFormThatWasMet()
    {
        await SendAsync(Command("une randonnée,,preply,,,,en,a hike,,\n", listName: "Preply"));

        var encounter = (await ListAsync<WordEncounter>()).Single();

        encounter.SourceIdentifier.Should().Be("Preply:une randonnée");
        encounter.Context.Should().Be("Preply");
    }
}
