using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Commands;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.UnitTests.Study;

/// <summary>
/// Throwing study progress away.
///
/// This deletes rows, so what it spares matters as much as what it removes: the words
/// themselves, and any content generated for them, which cost a model call to produce.
/// </summary>
public class ClearStudyProgressTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private StudyTestContext _db = null!;
    private FakeTimeProvider _clock = null!;
    private StudyOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new StudyTestContext();
        _clock = new FakeTimeProvider(Start);
        _options = new StudyOptions();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private ClearStudyProgressCommandHandler Handler() => new(_db.Context, _options, _clock);

    private Task<ClearStudyProgressResultDto> Clear(ClearStudyScope scope) =>
        Handler().Handle(new ClearStudyProgressCommand(Language.English, scope), CancellationToken.None);

    private async Task<ReviewCard> AddCard(
        string headword, double introducedDaysAgo, Language language = Language.English)
    {
        var word = new Word { Headword = headword, PartOfSpeech = "adjective", Language = language };
        _db.Context.Words.Add(word);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var card = new ReviewCard
        {
            WordId = word.Id,
            State = CardState.Review,
            CurrentRung = 2,
            IntervalDays = 5,
            IntroducedAtUtc = Start.UtcDateTime.AddDays(-introducedDaysAgo),
            DueAtUtc = Start.UtcDateTime
        };

        _db.Context.ReviewCards.Add(card);
        await _db.Context.SaveChangesAsync(CancellationToken.None);
        return card;
    }

    private async Task AddLog(ReviewCard card, double reviewedDaysAgo)
    {
        _db.Context.ReviewLogs.Add(new ReviewLog
        {
            ReviewCardId = card.Id,
            AttemptId = Guid.NewGuid(),
            ReviewedAtUtc = Start.UtcDateTime.AddDays(-reviewedDaysAgo),
            ExerciseType = ExerciseType.WordToMeaningChoice,
            Grade = ReviewGrade.Good,
            StateBefore = CardState.Review,
            EaseFactorAfter = 2.5
        });

        await _db.Context.SaveChangesAsync(CancellationToken.None);
    }

    [Test]
    public async Task RestartingTodayRemovesWordsFirstMetToday()
    {
        var today = await AddCard("metToday", introducedDaysAgo: 0);
        await AddLog(today, reviewedDaysAgo: 0);

        var result = await Clear(ClearStudyScope.Today);

        result.CardsRemoved.Should().Be(1);
        (await _db.Context.ReviewCards.CountAsync()).Should().Be(0);
        (await _db.Context.ReviewLogs.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task RestartingTodayLeavesOlderWordsInPlace()
    {
        await AddCard("metLastWeek", introducedDaysAgo: 7);

        await Clear(ClearStudyScope.Today);

        (await _db.Context.ReviewCards.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task RestartingTodayRemovesOnlyTodaysReviewsFromAnOlderWord()
    {
        var older = await AddCard("metLastWeek", introducedDaysAgo: 7);
        await AddLog(older, reviewedDaysAgo: 3);
        await AddLog(older, reviewedDaysAgo: 0);

        var result = await Clear(ClearStudyScope.Today);

        result.ReviewsRemoved.Should().Be(1);
        (await _db.Context.ReviewLogs.CountAsync()).Should().Be(1, "the older review is history, not today");
    }

    [Test]
    public async Task RestartingTodaySaysWhichOlderWordsItCouldNotRewind()
    {
        // An older word answered today keeps the state those answers left it in: the ease
        // and success average before them are not recorded per review.
        var older = await AddCard("metLastWeek", introducedDaysAgo: 7);
        await AddLog(older, reviewedDaysAgo: 0);

        var result = await Clear(ClearStudyScope.Today);

        result.CardsLeftAdvanced.Should().Be(1);
        (await _db.Context.ReviewCards.SingleAsync()).CurrentRung.Should().Be(2);
    }

    [Test]
    public async Task ClearingEverythingLeavesNothingStudied()
    {
        var today = await AddCard("metToday", introducedDaysAgo: 0);
        var older = await AddCard("metLastWeek", introducedDaysAgo: 7);
        await AddLog(today, reviewedDaysAgo: 0);
        await AddLog(older, reviewedDaysAgo: 3);

        var result = await Clear(ClearStudyScope.All);

        result.CardsRemoved.Should().Be(2);
        result.ReviewsRemoved.Should().Be(2);
        (await _db.Context.ReviewCards.CountAsync()).Should().Be(0);
        (await _db.Context.ReviewLogs.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task TheWordsThemselvesAreNeverTouched()
    {
        await AddCard("keepme", introducedDaysAgo: 0);

        await Clear(ClearStudyScope.All);

        (await _db.Context.Words.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task GeneratedContentIsKeptBecauseItCostAModelCall()
    {
        var card = await AddCard("expensive", introducedDaysAgo: 0);

        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = card.WordId,
            Status = StudyContentStatus.Ready,
            GeneratedDefinition = "worth keeping"
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        await Clear(ClearStudyScope.All);

        (await _db.Context.WordStudyContents.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task AnotherLanguageIsLeftAlone()
    {
        await AddCard("englishword", introducedDaysAgo: 0);
        await AddCard("motfrancais", introducedDaysAgo: 0, language: Language.French);

        await Clear(ClearStudyScope.All);

        var remaining = await _db.Context.ReviewCards.Include(c => c.Word).SingleAsync();
        remaining.Word.Headword.Should().Be("motfrancais");
    }

    [Test]
    public async Task ClearingAnUnstudiedCollectionIsHarmless()
    {
        var result = await Clear(ClearStudyScope.All);

        result.CardsRemoved.Should().Be(0);
        result.ReviewsRemoved.Should().Be(0);
    }
}
