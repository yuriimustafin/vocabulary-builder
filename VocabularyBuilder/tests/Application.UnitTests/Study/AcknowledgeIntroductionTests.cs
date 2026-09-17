using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Commands;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Scheduling;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.UnitTests.Study;

/// <summary>
/// Meeting a word for the first time.
///
/// Nothing here may look like evidence of how well the word is known: the learner has read
/// it, not recalled it. A grade at this point would be a guess the scheduler then treats as
/// fact, which is why the first showing takes its own path.
/// </summary>
public class AcknowledgeIntroductionTests
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

    private AcknowledgeIntroductionCommandHandler Handler() =>
        new(_db.Context, new Sm2Scheduler(_options, new Random(1)), new ConfiguredExerciseLadder(_options), _clock);

    private async Task<ReviewCard> AddNewCard()
    {
        var word = new Word { Headword = "ubiquitous", PartOfSpeech = "adjective", Language = Language.English };
        _db.Context.Words.Add(word);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var card = new ReviewCard
        {
            WordId = word.Id,
            State = CardState.New,
            CurrentRung = 0,
            IntroducedAtUtc = Start.UtcDateTime,
            DueAtUtc = Start.UtcDateTime
        };

        _db.Context.ReviewCards.Add(card);
        await _db.Context.SaveChangesAsync(CancellationToken.None);
        return card;
    }

    private Task<IntroductionResultDto> Acknowledge(int cardId, Guid? attemptId = null) =>
        Handler().Handle(
            new AcknowledgeIntroductionCommand
            {
                CardId = cardId,
                AttemptId = attemptId ?? Guid.NewGuid(),
                ExerciseType = ExerciseType.WordToMeaningReveal,
                ElapsedMs = 3000
            },
            CancellationToken.None);

    private Task<ReviewCard> Reload(int cardId) =>
        _db.Context.ReviewCards.AsNoTracking().FirstAsync(c => c.Id == cardId);

    [Test]
    public async Task TheWordGoesOntoTheFirstLearningStep()
    {
        var card = await AddNewCard();

        var result = await Acknowledge(card.Id);

        result.State.Should().Be(CardState.Learning);
        result.NextDueAtUtc.Should().Be(Start.UtcDateTime.AddMinutes(1));
    }

    [Test]
    public async Task TheNextTimeTheWordComesRoundItIsActuallyTested()
    {
        var card = await AddNewCard();

        await Acknowledge(card.Id);

        // Rung 1 is the first multiple-choice question, so a minute later the word is being
        // asked about rather than shown.
        (await Reload(card.Id)).CurrentRung.Should().Be(1);
    }

    [Test]
    public async Task NothingAboutTheCardsRecordIsTouched()
    {
        var card = await AddNewCard();

        await Acknowledge(card.Id);

        var after = await Reload(card.Id);

        after.EaseFactor.Should().Be(2.5, "reading a word says nothing about how easy it is");
        after.RecentSuccessRate.Should().Be(1.0);
        after.Lapses.Should().Be(0);
        after.LapsesSinceRecovery.Should().Be(0);
        after.ReviewNumber.Should().Be(0, "it has not been reviewed yet, only met");
    }

    [Test]
    public async Task ItIsRecordedAsSupportRatherThanAsAReview()
    {
        var card = await AddNewCard();

        await Acknowledge(card.Id);

        var log = await _db.Context.ReviewLogs.AsNoTracking().SingleAsync();

        log.IsScaffold.Should().BeTrue("an introduction must stay out of every statistic that judges recall");
        log.GradeWasSelfReported.Should().BeFalse("the learner was not asked to judge anything");
    }

    [Test]
    public async Task AcknowledgingTwiceCountsOnce()
    {
        var card = await AddNewCard();
        var attemptId = Guid.NewGuid();

        var first = await Acknowledge(card.Id, attemptId);
        var after = await Reload(card.Id);

        var second = await Acknowledge(card.Id, attemptId);
        var later = await Reload(card.Id);

        first.WasDuplicate.Should().BeFalse();
        second.WasDuplicate.Should().BeTrue();

        later.CurrentRung.Should().Be(after.CurrentRung);
        later.DueAtUtc.Should().Be(after.DueAtUtc);
        (await _db.Context.ReviewLogs.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task TheRungNeverClimbsPastTheTopOfTheLadder()
    {
        var card = await AddNewCard();

        var tracked = await _db.Context.ReviewCards.FirstAsync(c => c.Id == card.Id);
        tracked.CurrentRung = new ConfiguredExerciseLadder(_options).RungCount - 1;
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        await Acknowledge(card.Id);

        (await Reload(card.Id)).CurrentRung.Should().Be(new ConfiguredExerciseLadder(_options).RungCount - 1);
    }

    [Test]
    public async Task AWordLeftUnacknowledgedIsStillWaitingToBeMet()
    {
        // Closing the page part-way through should not quietly count the word as introduced.
        var card = await AddNewCard();

        (await Reload(card.Id)).State.Should().Be(CardState.New);

        await Acknowledge(card.Id);

        (await Reload(card.Id)).State.Should().Be(CardState.Learning);
    }
}
