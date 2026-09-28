using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.FunctionalTests.Words.Commands;

using static Testing;

/// <summary>
/// Filling a bare headword from the dictionary, which is what gives a French noun the gender
/// its article is derived from.
/// </summary>
/// <remarks>
/// Runs against the recorded WordReference pages under MockData, so the parsing is real and
/// the network is not touched. Every headword used here has a recorded page.
/// </remarks>
public class FillWordFromDictionaryTests : BaseTestFixture
{
    private static async Task<int> GivenBareHeadword(string headword)
    {
        var word = new Word { Headword = headword, Language = Language.French };

        await AddAsync(word);

        return word.Id;
    }

    private static async Task<Word> Reload(int id) =>
        (await ListAsync<Word>()).Single(w => w.Id == id);

    /// <summary>
    /// The case from the study session: imported as a bare headword, studied without an
    /// article because nothing had ever looked it up.
    /// </summary>
    [Test]
    public async Task ShouldGiveANounItsGenderAndWithItAnArticle()
    {
        var id = await GivenBareHeadword("maison");

        var outcome = await SendAsync(new FillWordFromDictionaryCommand(id));

        outcome.Should().Be(DictionaryFillOutcome.Filled);

        var word = await Reload(id);

        word.Gender.Should().Be(GrammaticalGender.Feminine);
        word.GetArticle().Should().NotBeNull();
        word.GetArticle()!.Indefinite.Should().Be("une");
        word.GetArticle()!.Definite.Should().Be("la");
    }

    [Test]
    public async Task ShouldRecordWhatOnlyTheDictionaryKnows()
    {
        var id = await GivenBareHeadword("maison");

        await SendAsync(new FillWordFromDictionaryCommand(id));

        var word = await Reload(id);

        word.PartOfSpeech.Should().NotBeNullOrWhiteSpace();
        word.Transcription.Should().NotBeNullOrWhiteSpace();

        // Senses live in their own table and Reload does not join them
        (await CountAsync<Sense>()).Should().BeGreaterThan(0);
    }

    /// <summary>
    /// An aspirated h blocks elision, and the mark the dictionary writes it with is carried
    /// on the transcription - so filling the word is what lets "la hache" come out right.
    /// </summary>
    [Test]
    public async Task ShouldFillAWordWhoseArticleDependsOnItsPronunciation()
    {
        var id = await GivenBareHeadword("hache");

        await SendAsync(new FillWordFromDictionaryCommand(id));

        var word = await Reload(id);

        word.GetArticle()!.Definite.Should().Be("la", "the h in hache is aspirated, so it does not elide");
        word.GetArticle()!.IsElided.Should().BeFalse();
    }

    [Test]
    public async Task ShouldElideTheArticleOfAVowelInitialNoun()
    {
        var id = await GivenBareHeadword("arbre");

        await SendAsync(new FillWordFromDictionaryCommand(id));

        (await Reload(id)).GetArticle()!.Definite.Should().Be("l'");
    }

    /// <summary>
    /// Filling is idempotent, so the sweep can be run as often as anyone likes and a word
    /// already filled costs no request.
    /// </summary>
    [Test]
    public async Task ShouldLeaveAWordAloneOnceItIsFilled()
    {
        var id = await GivenBareHeadword("maison");

        await SendAsync(new FillWordFromDictionaryCommand(id));
        var outcome = await SendAsync(new FillWordFromDictionaryCommand(id));

        outcome.Should().Be(DictionaryFillOutcome.AlreadyFilled);
    }

    /// <summary>
    /// Looking a word up is not meeting it, so the fill must not inflate how well known the
    /// word looks.
    /// </summary>
    [Test]
    public async Task ShouldNotCountAsAnotherEncounter()
    {
        var id = await GivenBareHeadword("maison");

        await SendAsync(new FillWordFromDictionaryCommand(id));
        await SendAsync(new FillWordFromDictionaryCommand(id));

        var encounters = await ListAsync<WordEncounter>();

        encounters.Where(e => e.WordId == id).Should().BeEmpty();
    }

    /// <summary>
    /// Asked in English, because French cannot reach this outcome: WordReference falls back to
    /// a model, and a model answers for anything put in front of it. English has no fallback,
    /// so a word the dictionary does not carry is simply not found.
    /// </summary>
    [Test]
    public async Task ShouldReportAWordTheDictionaryDoesNotHave()
    {
        var word = new Word { Headword = "zzzznotaword", Language = Language.English };
        await AddAsync(word);

        var outcome = await SendAsync(new FillWordFromDictionaryCommand(word.Id));

        outcome.Should().Be(DictionaryFillOutcome.NotFound);
    }

    [Test]
    public async Task ShouldSayWhenThereIsNoSuchWord()
    {
        (await SendAsync(new FillWordFromDictionaryCommand(9999)))
            .Should().Be(DictionaryFillOutcome.WordNotFound);
    }

    /// <summary>
    /// The sweep exists for words a study session will not reach for months.
    /// </summary>
    [Test]
    public async Task ShouldSweepEveryWordStillWaiting()
    {
        await GivenBareHeadword("maison");
        await GivenBareHeadword("arbre");
        await GivenBareHeadword("hache");

        var result = await SendAsync(new FillMissingDictionaryDataCommand(Language.French));

        result.Considered.Should().Be(3);
        result.Filled.Should().Be(3);
        result.FilledWords.Should().BeEquivalentTo(new[] { "maison", "arbre", "hache" });

        var words = await ListAsync<Word>();
        words.Should().OnlyContain(w => w.Gender != null);
    }

    [Test]
    public async Task ShouldSweepNothingWhenEveryWordIsFilled()
    {
        await GivenBareHeadword("maison");
        await SendAsync(new FillMissingDictionaryDataCommand(Language.French));

        var again = await SendAsync(new FillMissingDictionaryDataCommand(Language.French));

        again.Considered.Should().Be(0);
        again.Filled.Should().Be(0);
    }

    /// <summary>
    /// Each language is looked up in its own dictionary, so a sweep of one must not reach
    /// into the other.
    /// </summary>
    [Test]
    public async Task ShouldLeaveAnotherLanguageAlone()
    {
        await AddAsync(new Word { Headword = "house", Language = Language.English });
        await GivenBareHeadword("maison");

        var result = await SendAsync(new FillMissingDictionaryDataCommand(Language.French));

        result.Considered.Should().Be(1);
        result.FilledWords.Should().Equal("maison");
    }
}
