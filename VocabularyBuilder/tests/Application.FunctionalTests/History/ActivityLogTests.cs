using System.Text.Json;
using VocabularyBuilder.Application.History.Queries;
using VocabularyBuilder.Application.Lists.Commands;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.History;

using static Testing;

/// <summary>
/// The activity log, written by the actions themselves in the same save as the change.
/// </summary>
public class ActivityLogTests : BaseTestFixture
{
    private static async Task<int> GivenWord(string headword = "maison")
    {
        return await SendAsync(new CreateWordCommand { Headword = headword, Language = Language.French });
    }

    private static async Task<List<ActivityLogEntry>> Log() =>
        (await ListAsync<ActivityLogEntry>()).OrderBy(e => e.Id).ToList();

    [Test]
    public async Task ShouldRecordAWordBeingCreated()
    {
        var id = await GivenWord();

        var entry = (await Log()).Single();

        entry.Action.Should().Be(ActivityAction.WordCreated);
        entry.WordId.Should().Be(id);
        entry.Headword.Should().Be("maison");
        entry.Language.Should().Be(Language.French);
    }

    [Test]
    public async Task ShouldRecordAStatusChangeFromAndTo()
    {
        var id = await GivenWord();

        await SendAsync(new UpdateWordStatusCommand { Id = id, Status = WordStatus.Learned });

        var entry = (await Log()).Last();

        entry.Action.Should().Be(ActivityAction.WordStatusChanged);
        entry.Summary.Should().Be("New → Learned");
    }

    [Test]
    public async Task ShouldNotRecordAStatusSetToWhatItAlreadyWas()
    {
        var id = await GivenWord();

        await SendAsync(new UpdateWordStatusCommand { Id = id, Status = WordStatus.New });

        (await Log()).Should().ContainSingle(e => e.Action == ActivityAction.WordCreated);
        (await Log()).Should().HaveCount(1);
    }

    [Test]
    public async Task ShouldRecordOnlyTheFieldsAnEditChanged()
    {
        var id = await GivenWord();

        await SendAsync(new UpdateWordCommand
        {
            Id = id,
            Headword = "maison",
            Transcription = "mɛzɔ̃",
            Gender = GrammaticalGender.Feminine
        });

        var entry = (await Log()).Last();

        entry.Action.Should().Be(ActivityAction.WordUpdated);

        using var details = JsonDocument.Parse(entry.Details!);
        details.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("Transcription", "Gender");
        details.RootElement.GetProperty("Transcription").GetProperty("to").GetString().Should().Be("mɛzɔ̃");
    }

    /// <summary>The whole point of a history: it outlives the word.</summary>
    [Test]
    public async Task ShouldKeepTheWordsHistoryAfterTheWordIsDeleted()
    {
        var id = await GivenWord();

        await SendAsync(new DeleteWordCommand(id));

        (await ListAsync<Word>()).Should().BeEmpty();

        var log = await Log();
        log.Select(e => e.Action).Should().Equal(ActivityAction.WordCreated, ActivityAction.WordDeleted);
        log.Should().OnlyContain(e => e.WordId == id && e.Headword == "maison");
    }

    [Test]
    public async Task ShouldRecordAWordBeingFilledFromTheDictionary()
    {
        var word = new Word { Headword = "maison", Language = Language.French };
        await AddAsync(word);

        await SendAsync(new FillWordFromDictionaryCommand(word.Id));

        var entry = (await Log()).Single(e => e.Action == ActivityAction.WordFilledFromDictionary);

        entry.WordId.Should().Be(word.Id);
        entry.Summary.Should().StartWith("Filled from Gpt");
    }

    [Test]
    public async Task ShouldRecordListChanges()
    {
        var listId = await SendAsync(new CreateListCommand { Title = "Kitchen", Language = Language.French });
        await SendAsync(new CreateListItemCommand { ListId = listId, Text = "une casserole" });
        await SendAsync(new DeleteListCommand(listId));

        (await Log()).Select(e => e.Action).Should().Equal(
            ActivityAction.ListCreated, ActivityAction.ListItemAdded, ActivityAction.ListDeleted);
    }

    [Test]
    public async Task ShouldFilterTheLogByCategoryAndWord()
    {
        var id = await GivenWord();
        await GivenWord("arbre");
        await SendAsync(new UpdateWordStatusCommand { Id = id, Status = WordStatus.Learned });
        await SendAsync(new CreateListCommand { Title = "Kitchen", Language = Language.French });

        var words = await SendAsync(new GetActivityLogQuery { Language = Language.French, Category = "Words" });
        words.Items.Should().HaveCount(3);

        var forWord = await SendAsync(new GetActivityLogQuery { Language = Language.French, WordId = id });
        forWord.Items.Select(e => e.Action).Should().Equal("WordStatusChanged", "WordCreated");

        var english = await SendAsync(new GetActivityLogQuery { Language = Language.English });
        english.Items.Should().BeEmpty();
    }

    [Test]
    public async Task ShouldNotShowOneUsersActivityToAnother()
    {
        await GivenWord();

        await RunAsUserAsync("someone-else@local", "Testing1234!", Array.Empty<string>());

        (await SendAsync(new GetActivityLogQuery { Language = Language.French })).Items.Should().BeEmpty();
    }
}
