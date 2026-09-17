using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Scheduling;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class Sm2SchedulerTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Fuzz off by default so intervals are exact; a dedicated test covers the spread.</summary>
    private static StudyOptions Options() => new() { IntervalFuzzPercent = 0 };

    private static Sm2Scheduler Scheduler(StudyOptions? options = null) =>
        new(options ?? Options(), new Random(1));

    private static ReviewCard Card(CardState state = CardState.New, int interval = 0,
        int stepsCompleted = 0, double ease = 2.5) => new()
        {
            WordId = 1,
            State = state,
            IntervalDays = interval,
            LearningStepIndex = stepsCompleted,
            EaseFactor = ease
        };

    [Test]
    public void NewCardWalksTheLearningStepsInOrderThenGraduates()
    {
        var scheduler = Scheduler();
        var card = Card();

        // Three same-day touches is what puts the first three exercise types in one session.
        var first = scheduler.Schedule(card, ReviewGrade.Good, Now);
        first.State.Should().Be(CardState.Learning);
        first.DueAtUtc.Should().Be(Now.AddMinutes(1));

        card.LearningStepIndex = first.LearningStepIndex;
        var second = scheduler.Schedule(card, ReviewGrade.Good, Now);
        second.State.Should().Be(CardState.Learning);
        second.DueAtUtc.Should().Be(Now.AddMinutes(10));

        card.LearningStepIndex = second.LearningStepIndex;
        var third = scheduler.Schedule(card, ReviewGrade.Good, Now);
        third.State.Should().Be(CardState.Review);
        third.IntervalDays.Should().Be(1);
        third.DueAtUtc.Should().Be(Now.AddDays(1));
    }

    [Test]
    public void AgainDuringLearningReturnsToTheFirstStep()
    {
        var result = Scheduler().Schedule(Card(CardState.Learning, stepsCompleted: 2), ReviewGrade.Again, Now);

        result.State.Should().Be(CardState.Learning);
        result.LearningStepIndex.Should().Be(0);
        result.DueAtUtc.Should().Be(Now.AddMinutes(1));
    }

    [Test]
    public void HardDuringLearningRepeatsTheCurrentStep()
    {
        var result = Scheduler().Schedule(Card(CardState.Learning, stepsCompleted: 1), ReviewGrade.Hard, Now);

        result.LearningStepIndex.Should().Be(1);
        result.DueAtUtc.Should().Be(Now.AddMinutes(1));
    }

    [Test]
    public void EasySkipsTheRemainingLearningSteps()
    {
        var result = Scheduler().Schedule(Card(), ReviewGrade.Easy, Now);

        result.State.Should().Be(CardState.Review);
        result.IntervalDays.Should().Be(4);
    }

    [Test]
    public void ReviewIntervalGrowsByTheEaseFactor()
    {
        var result = Scheduler().Schedule(Card(CardState.Review, interval: 10, ease: 2.5), ReviewGrade.Good, Now);

        result.IntervalDays.Should().Be(25);
        result.EaseFactor.Should().Be(2.5);
        result.DueAtUtc.Should().Be(Now.AddDays(25));
    }

    [Test]
    public void HardShrinksEaseAndGrowsTheIntervalOnlyGently()
    {
        var result = Scheduler().Schedule(Card(CardState.Review, interval: 10, ease: 2.5), ReviewGrade.Hard, Now);

        result.EaseFactor.Should().BeApproximately(2.35, 1e-9);
        result.IntervalDays.Should().Be(12);
    }

    [Test]
    public void EasyRaisesEaseAndAppliesTheBonus()
    {
        var result = Scheduler().Schedule(Card(CardState.Review, interval: 10, ease: 2.5), ReviewGrade.Easy, Now);

        result.EaseFactor.Should().BeApproximately(2.65, 1e-9);
        result.IntervalDays.Should().Be(34); // 10 * 2.65 * 1.3
    }

    [Test]
    public void AnIntervalAlwaysMovesForwardEvenAtTheMinimumEase()
    {
        // At ease 1.3 a one-day interval would otherwise round straight back to one day.
        var result = Scheduler().Schedule(Card(CardState.Review, interval: 1, ease: 1.3), ReviewGrade.Good, Now);

        result.IntervalDays.Should().BeGreaterThan(1);
    }

    [Test]
    public void FailingAReviewLapsesTheCardIntoRelearning()
    {
        var result = Scheduler().Schedule(Card(CardState.Review, interval: 30, ease: 2.5), ReviewGrade.Again, Now);

        result.State.Should().Be(CardState.Relearning);
        result.IsLapse.Should().BeTrue();
        result.EaseFactor.Should().BeApproximately(2.30, 1e-9);
        result.IntervalDays.Should().Be(1); // LapseIntervalPercent 0 restarts from a day
        result.DueAtUtc.Should().Be(Now.AddMinutes(1));
    }

    [Test]
    public void ARelearningCardReturnsToItsRetainedIntervalOnGraduation()
    {
        var options = Options();
        options.LapseIntervalPercent = 50;
        var scheduler = Scheduler(options);

        var lapse = scheduler.Schedule(Card(CardState.Review, interval: 30, ease: 2.5), ReviewGrade.Again, Now);
        lapse.IntervalDays.Should().Be(15);

        var card = Card(CardState.Relearning, interval: lapse.IntervalDays, stepsCompleted: 2, ease: lapse.EaseFactor);
        var graduated = scheduler.Schedule(card, ReviewGrade.Good, Now);

        graduated.State.Should().Be(CardState.Review);
        graduated.IntervalDays.Should().Be(15);
    }

    [Test]
    public void EaseNeverFallsBelowTheConfiguredFloor()
    {
        var result = Scheduler().Schedule(Card(CardState.Review, interval: 5, ease: 1.3), ReviewGrade.Again, Now);

        result.EaseFactor.Should().Be(1.3);
    }

    [Test]
    public void IntervalsAreCappedAtTheMaximum()
    {
        var result = Scheduler().Schedule(Card(CardState.Review, interval: 300, ease: 2.5), ReviewGrade.Easy, Now);

        result.IntervalDays.Should().Be(365);
    }

    [Test]
    public void FuzzKeepsIntervalsWithinTheConfiguredSpread()
    {
        var options = new StudyOptions { IntervalFuzzPercent = 5 };
        var scheduler = new Sm2Scheduler(options, new Random(42));

        var results = Enumerable.Range(0, 200)
            .Select(_ => scheduler.Schedule(Card(CardState.Review, interval: 100, ease: 2.5), ReviewGrade.Good, Now))
            .Select(r => r.IntervalDays)
            .ToList();

        // Unfuzzed value is 250; 5% either side allows 237..263 once rounding is included.
        results.Should().OnlyContain(i => i >= 237 && i <= 263);
        results.Distinct().Should().HaveCountGreaterThan(1, "fuzz should actually spread the cohort");
    }
}
