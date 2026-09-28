using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.Words.Commands;

using static Testing;

/// <summary>
/// Looking a word up again on purpose, which is what carries a field the parser has only just
/// started asking for onto words collected before it.
/// </summary>
/// <remarks>
/// Re-parsing a cached page cannot do it: the page already stored is all re-parsing has, and a
/// gloss recorded before the prompt asked for one is not in it to find. So a forced fill has to
/// ignore both the "already filled" gate and the cache.
/// </remarks>
public class ForcedDictionaryRefillTests : BaseTestFixture
{
    private static async Task<Word> Reload(int id) =>
        (await ListAsync<Word>()).Single(w => w.Id == id);

    private static async Task<int> GivenAFilledWordWithNoGloss()
    {
        var word = new Word
        {
            Headword = "maison",
            Language = Language.French,
            PartOfSpeech = "noun",
            Gender = GrammaticalGender.Feminine,
            Transcription = "mɛ.zɔ̃",
            Senses = new List<Sense>
            {
                // What an older parser stored: a meaning, and no gloss beside it
                new() { Definition = "house, home", Gloss = null, Examples = new List<string>() }
            }
        };

        await AddAsync(word);

        return word.Id;
    }

    [Test]
    public async Task ShouldLeaveAFilledWordAloneWithoutForce()
    {
        var id = await GivenAFilledWordWithNoGloss();

        var outcome = await SendAsync(new FillWordFromDictionaryCommand(id));

        outcome.Should().Be(DictionaryFillOutcome.AlreadyFilled);
        (await CountAsync<Sense>()).Should().Be(1);
    }

    [Test]
    public async Task ShouldLookAFilledWordUpAgainWhenForced()
    {
        var id = await GivenAFilledWordWithNoGloss();

        var outcome = await SendAsync(new FillWordFromDictionaryCommand(id, Force: true));

        outcome.Should().Be(DictionaryFillOutcome.Filled);

        // Sense keeps only a shadow key back to its word, and this test has the one word
        var senses = await ListAsync<Sense>();

        senses.Should().Contain(s => s.Gloss != null,
            "the recorded answer carries a French gloss, which is the point of asking again");
    }

    /// <summary>
    /// The senses are replaced, not added to. UpsertWord merges - it keeps any sense whose
    /// definition it has not seen - and on a re-lookup that is nearly all of them, because the
    /// new answer words the same meaning slightly differently. Left merging, every forced sweep
    /// leaves the collection fuller of near duplicates than it found it.
    /// </summary>
    [Test]
    public async Task ShouldReplaceTheSensesRatherThanAddToThemWhenForced()
    {
        var id = await GivenAFilledWordWithNoGloss();

        await SendAsync(new FillWordFromDictionaryCommand(id, Force: true));

        var senses = await ListAsync<Sense>();

        senses.Should().NotContain(s => s.Definition == "house, home" && s.Gloss == null,
            "the sense that was there before the lookup is gone, not sitting beside its replacement");
        senses.Should().HaveCount(2, "which is what the recorded answer for maison carries");
    }

    /// <summary>
    /// The sweep only considers words that are waiting, unless it is told to take every one.
    /// </summary>
    [Test]
    public async Task ShouldSweepOnlyWaitingWordsWithoutForce()
    {
        await GivenAFilledWordWithNoGloss();

        var result = await SendAsync(new FillMissingDictionaryDataCommand(Language.French));

        result.Considered.Should().Be(0);
    }

    [Test]
    public async Task ShouldSweepEveryWordWhenForced()
    {
        await GivenAFilledWordWithNoGloss();

        var result = await SendAsync(new FillMissingDictionaryDataCommand(Language.French, Force: true));

        result.Considered.Should().Be(1);
        result.Filled.Should().Be(1);
    }

    /// <summary>
    /// A limit is what makes a forced sweep usable on a real collection: every word is a request,
    /// and where the dictionary is a model every word is a bill.
    /// </summary>
    [Test]
    public async Task ShouldHonourTheLimitOnAForcedSweep()
    {
        await GivenAFilledWordWithNoGloss();
        await AddAsync(new Word
        {
            Headword = "livre",
            Language = Language.French,
            PartOfSpeech = "noun",
            Gender = GrammaticalGender.Masculine,
            Senses = new List<Sense> { new() { Definition = "book", Examples = new List<string>() } }
        });

        var result = await SendAsync(
            new FillMissingDictionaryDataCommand(Language.French, Limit: 1, Force: true));

        result.Considered.Should().Be(1);
    }

    /// <summary>
    /// Where a result is filed. The dictionary declines a word named to fail, so the language's
    /// fallback answers - and its page has to be cached as the fallback's, not as the source that
    /// was asked for. Filing it by the URL's shape got this right only while GPT was the
    /// fallback, and silently wrong the moment the pair was inverted.
    /// </summary>
    [Test]
    public async Task ShouldCacheAFallbackResultUnderTheSourceThatProducedIt()
    {
        var results = await SendAsync(new LookupWordsFromDictionaryQuery
        {
            Words = new List<string> { "zzfailmaison" },
            Language = Language.French,
            SourceType = Language.French.GetDefaultSourceType()
        });

        var found = results.Should().ContainSingle().Which;

        found.DictionarySources.Should().ContainSingle().Which.SourceType
            .Should().Be(DictionarySourceType.WordReference,
                "WordReference answered, so the page is WordReference's - not the GPT source that was asked for");

        found.Word.Gender.Should().Be(GrammaticalGender.Feminine,
            "and the entry itself came from that page");
    }

    [Test]
    public void ShouldFileAConjugationTableUnderItsOwnSourcesType()
    {
        LookupWordsFromDictionaryQueryHandler
            .ConjugationSourceFor(DictionarySourceType.Gpt)
            .Should().Be(DictionarySourceType.GptConjugation);

        LookupWordsFromDictionaryQueryHandler
            .ConjugationSourceFor(DictionarySourceType.WordReference)
            .Should().Be(DictionarySourceType.WordReferenceConjugation);
    }
}
