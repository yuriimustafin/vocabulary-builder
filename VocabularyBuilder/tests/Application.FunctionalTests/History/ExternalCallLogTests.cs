using VocabularyBuilder.Application.History.Queries;
using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.History;

using static Testing;

/// <summary>
/// The log of every model and dictionary request, against the recorded clients - which the
/// test host wraps for recording exactly as the application wraps the real ones.
/// </summary>
public class ExternalCallLogTests : BaseTestFixture
{
    [Test]
    public async Task ShouldRecordTheModelCallThatFilledAWord()
    {
        var word = new Word { Headword = "maison", Language = Language.French };
        await AddAsync(word);

        await SendAsync(new FillWordFromDictionaryCommand(word.Id));

        var call = (await ListAsync<ExternalCallLog>()).First(c => c.Purpose == ExternalCallPurpose.DictionaryEntry);

        call.Provider.Should().Be(ExternalCallProvider.Gpt);
        call.WordId.Should().Be(word.Id);
        call.Target.Should().Be("maison");
        call.Request.Should().Contain("\"maison\"");
        call.Response.Should().NotBeNullOrEmpty();
        call.Succeeded.Should().BeTrue();
        call.IsMock.Should().BeTrue();
        call.OwnerId.Should().Be(GetUserId());
    }

    /// <summary>
    /// The model is asked twice for a verb - its entry, then its conjugation - and each call
    /// is its own row.
    /// </summary>
    [Test]
    public async Task ShouldRecordAConjugationAsACallOfItsOwn()
    {
        var word = new Word { Headword = "parler", Language = Language.French };
        await AddAsync(word);

        await SendAsync(new FillWordFromDictionaryCommand(word.Id));

        var calls = await ListAsync<ExternalCallLog>();

        calls.Should().Contain(c => c.Purpose == ExternalCallPurpose.DictionaryEntry && c.WordId == word.Id);
        calls.Should().Contain(c => c.Purpose == ExternalCallPurpose.Conjugation && c.WordId == word.Id);
    }

    /// <summary>
    /// A call made during an import is filed under it, including the ones made before any word
    /// existed to file it under.
    /// </summary>
    [Test]
    public async Task ShouldFileTheCallsAnImportMadeUnderTheImport()
    {
        var result = await SendAsync(new ImportLessonNotesCommand
        {
            Notes = "un pas=step\nun serpent=snake",
            Language = Language.French,
            ListName = "lesson"
        });

        var calls = await ListAsync<ExternalCallLog>();

        var extraction = calls.Single(c => c.Purpose == ExternalCallPurpose.NotesExtraction);
        extraction.ImportId.Should().Be(result.ImportId);
        extraction.Request.Should().Contain("un serpent=snake");

        var details = await SendAsync(new Application.Imports.Queries.GetImportDetailsQuery(result.ImportId!.Value));
        details!.ExternalCalls.Calls.Should().Be(calls.Count(c => c.ImportId == result.ImportId));
    }

    [Test]
    public async Task ShouldListCallsWithoutTheirBodiesAndOpenOneWithThem()
    {
        var word = new Word { Headword = "maison", Language = Language.French };
        await AddAsync(word);
        await SendAsync(new FillWordFromDictionaryCommand(word.Id));

        var page = await SendAsync(new GetExternalCallsQuery { Provider = ExternalCallProvider.Gpt });

        var listed = page.Items.First();
        listed.HasBody.Should().BeTrue();
        listed.Should().NotBeOfType<ExternalCallDetailsDto>();

        var opened = await SendAsync(new GetExternalCallQuery(listed.Id));
        opened!.Request.Should().Contain("maison");
        opened.Response.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task ShouldGatherAWordsImportsActivityAndCallsIntoItsHistory()
    {
        await SendAsync(new ImportLingQWordsCommand
        {
            FileContent = "term,phrase,tag1,tag2,tag3,tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2\nune maison,,,,,,,,,\n",
            Language = Language.French,
            ListName = "lesson 3"
        });

        var word = (await ListAsync<Word>()).Single();
        await SendAsync(new FillWordFromDictionaryCommand(word.Id));

        var history = await SendAsync(new GetWordHistoryQuery(word.Id));

        history!.Imports.Should().ContainSingle(i => i.Name == "lesson 3" && i.CreatedWord);
        history.Imports.Single().SourceTerms.Should().Equal("une maison");
        history.Activity.Should().Contain(e => e.Action == "WordFilledFromDictionary");
        history.Calls.Should().Contain(c => c.Purpose == "DictionaryEntry");
    }

    [Test]
    public async Task ShouldNotShowOneUsersCallsToAnother()
    {
        var word = new Word { Headword = "maison", Language = Language.French };
        await AddAsync(word);
        await SendAsync(new FillWordFromDictionaryCommand(word.Id));

        await RunAsUserAsync("someone-else@local", "Testing1234!", Array.Empty<string>());

        (await SendAsync(new GetExternalCallsQuery())).Items.Should().BeEmpty();
    }
}
