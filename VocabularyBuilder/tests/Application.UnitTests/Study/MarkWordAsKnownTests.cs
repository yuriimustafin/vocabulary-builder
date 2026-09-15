using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Commands;
using VocabularyBuilder.Application.Study.Queries;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.UnitTests.Study;

/// <summary>
/// Setting a word aside because it is already known.
///
/// Two things have to hold together: the day gets its place back, and the word does not
/// simply reappear tomorrow. A card is what records that a word has been seen, so removing
/// one without marking the word would achieve only the first.
/// </summary>
public class MarkWordAsKnownTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private StudyTestContext _db = null!;
    private StudyOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new StudyTestContext();
        _options = new StudyOptions();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private MarkWordAsKnownCommandHandler Handler() => new(_db.Context);

    private async Task<ReviewCard> AddIntroducedWord(string headword)
    {
        var word = new Word
        {
            Headword = headword,
            PartOfSpeech = "adjective",
            Language = Language.English,
            Frequency = 1000,
            Status = WordStatus.New
        };

        _db.Context.Words.Add(word);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var card = new ReviewCard
        {
            WordId = word.Id,
            State = CardState.New,
            IntroducedAtUtc = Start.UtcDateTime,
            DueAtUtc = Start.UtcDateTime
        };

        _db.Context.ReviewCards.Add(card);
        await _db.Context.SaveChangesAsync(CancellationToken.None);
        return card;
    }

    [Test]
    public async Task TheWordIsMarkedKnown()
    {
        var card = await AddIntroducedWord("obvious");

        (await Handler().Handle(new MarkWordAsKnownCommand(card.Id), CancellationToken.None))
            .Should().BeTrue();

        var word = await _db.Context.Words.AsNoTracking().SingleAsync();
        word.Status.Should().Be(WordStatus.Known);
    }

    [Test]
    public async Task TheCardIsRemovedSoTheDayGetsItsPlaceBack()
    {
        var card = await AddIntroducedWord("obvious");

        await Handler().Handle(new MarkWordAsKnownCommand(card.Id), CancellationToken.None);

        (await _db.Context.ReviewCards.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task TheWordItselfIsKept()
    {
        // Setting a word aside is not deleting it: it stays in the collection, just not in
        // the rotation.
        var card = await AddIntroducedWord("obvious");

        await Handler().Handle(new MarkWordAsKnownCommand(card.Id), CancellationToken.None);

        (await _db.Context.Words.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task AWordSetAsideIsNotOfferedAgain()
    {
        // The card is gone, so nothing else records that this word was ever seen. Without
        // the status it would simply be picked afresh.
        var card = await AddIntroducedWord("obvious");
        await Handler().Handle(new MarkWordAsKnownCommand(card.Id), CancellationToken.None);

        var offered = await NewWordSelector.SelectAsync(
            _db.Context, Language.English, 10, CancellationToken.None);

        offered.Should().BeEmpty();
    }

    [Test]
    public async Task AnotherWordCanTakeItsPlace()
    {
        var card = await AddIntroducedWord("obvious");
        await AddWordWithoutCard("replacement");

        await Handler().Handle(new MarkWordAsKnownCommand(card.Id), CancellationToken.None);

        var offered = await NewWordSelector.SelectAsync(
            _db.Context, Language.English, 10, CancellationToken.None);

        offered.Select(w => w.Headword).Should().Equal("replacement");
    }

    [Test]
    public async Task AWordQueuedForStudyIsUnqueued()
    {
        var card = await AddIntroducedWord("obvious");

        var word = await _db.Context.Words.FirstAsync();
        word.IsMarkedForStudy = true;
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        await Handler().Handle(new MarkWordAsKnownCommand(card.Id), CancellationToken.None);

        (await _db.Context.Words.AsNoTracking().SingleAsync()).IsMarkedForStudy.Should().BeFalse();
    }

    [Test]
    public async Task AnythingAlreadyRecordedAgainstTheCardGoesWithIt()
    {
        var card = await AddIntroducedWord("obvious");

        _db.Context.ReviewLogs.Add(new ReviewLog
        {
            ReviewCardId = card.Id,
            AttemptId = Guid.NewGuid(),
            ReviewedAtUtc = Start.UtcDateTime,
            ExerciseType = ExerciseType.WordToMeaningReveal,
            Grade = ReviewGrade.Good,
            IsScaffold = true
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        await Handler().Handle(new MarkWordAsKnownCommand(card.Id), CancellationToken.None);

        (await _db.Context.ReviewLogs.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task AnUnknownCardIsReportedRatherThanThrowing()
    {
        (await Handler().Handle(new MarkWordAsKnownCommand(4242), CancellationToken.None))
            .Should().BeFalse();
    }

    [Test]
    public async Task WordsMarkedKnownElsewhereAreNotStudiedEither()
    {
        // Whether the status was set here or on the words page, it means the same thing.
        var word = new Word
        {
            Headword = "longknown",
            Language = Language.English,
            PartOfSpeech = "adjective",
            Frequency = 500,
            Status = WordStatus.Known
        };

        _db.Context.Words.Add(word);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var offered = await NewWordSelector.SelectAsync(
            _db.Context, Language.English, 10, CancellationToken.None);

        offered.Should().BeEmpty();
    }

    private async Task AddWordWithoutCard(string headword)
    {
        _db.Context.Words.Add(new Word
        {
            Headword = headword,
            PartOfSpeech = "adjective",
            Language = Language.English,
            Frequency = 2000,
            Status = WordStatus.New
        });

        await _db.Context.SaveChangesAsync(CancellationToken.None);
    }
}
