using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class ExerciseLadderTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    private static ConfiguredExerciseLadder Ladder(StudyOptions? options = null) => new(options ?? new StudyOptions());

    private static ReviewCard Card(int rung = 0, int interval = 0, DateTime? lastReviewed = null) => new()
    {
        WordId = 1,
        CurrentRung = rung,
        IntervalDays = interval,
        LastReviewedAtUtc = lastReviewed
    };

    private static bool Anything(ExerciseType _) => true;

    [Test]
    public void TheDefaultLadderRunsEasiestToHardest()
    {
        var ladder = Ladder();

        ladder.RungCount.Should().Be(6);
        Enumerable.Range(0, 6).Select(ladder.TypeAt).Should().Equal(
            ExerciseType.WordToMeaningReveal,
            ExerciseType.WordToMeaningChoice,
            ExerciseType.MeaningToWordChoice,
            ExerciseType.ContextToWordRecall,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.MeaningToWordRecall);
    }

    [Test]
    public void SuccessClimbsARungWhileTheCardIsBeingRecalledReliably()
    {
        Ladder().NextRung(2, ReviewGrade.Good, recentSuccessRate: 0.9).Should().Be(3);
        Ladder().NextRung(2, ReviewGrade.Easy, recentSuccessRate: 0.9).Should().Be(3);
    }

    [Test]
    public void SuccessDoesNotClimbWhileTheSuccessRateIsBelowTarget()
    {
        // The 85% rule: a word being scraped through should not be made harder yet.
        Ladder().NextRung(2, ReviewGrade.Good, recentSuccessRate: 0.5).Should().Be(2);
    }

    [Test]
    public void HardHoldsTheRung()
    {
        Ladder().NextRung(3, ReviewGrade.Hard, recentSuccessRate: 1.0).Should().Be(3);
    }

    [Test]
    public void FailingDropsTheCardBackSoEarlierExercisesReturn()
    {
        // Failing cloze at rung 3 should bring the two multiple-choice rungs back.
        Ladder().NextRung(3, ReviewGrade.Again, recentSuccessRate: 1.0).Should().Be(1);
    }

    [Test]
    public void TheRungNeverFallsBelowZeroOrClimbsPastTheTop()
    {
        Ladder().NextRung(0, ReviewGrade.Again, 1.0).Should().Be(0);
        Ladder().NextRung(5, ReviewGrade.Good, 1.0).Should().Be(5);
    }

    [Test]
    public void TheProbeIsTheCardsOwnRungWhenNothingIsUnusual()
    {
        Ladder().SelectProbeRung(Card(rung: 2), "noun", Now, Anything).Should().Be(2);
    }

    [Test]
    public void ALongAbsenceEscalatesToUnhintedProduction()
    {
        // Eight days away from a three-day card: probe hard, and with no hint available,
        // so the grade measures memory rather than the cue.
        var card = Card(rung: 3, interval: 3, lastReviewed: Now.AddDays(-8));

        Ladder().SelectProbeRung(card, "noun", Now, Anything).Should().Be(5);
    }

    [Test]
    public void BeingWellOverdueEscalatesEvenWhenTheGapIsShort()
    {
        // Two days into a one-day interval is twice as long as intended.
        var card = Card(rung: 3, interval: 1, lastReviewed: Now.AddDays(-2));

        Ladder().SelectProbeRung(card, "noun", Now, Anything).Should().Be(5);
    }

    [Test]
    public void ALongAbsenceDoesNotEscalateAWordStillBeingLearned()
    {
        var card = Card(rung: 1, interval: 1, lastReviewed: Now.AddDays(-30));

        Ladder().SelectProbeRung(card, "noun", Now, Anything).Should().Be(1);
    }

    [Test]
    public void TheProbeFallsBackWhenTheChosenExerciseCannotBeBuilt()
    {
        // No distractor pool, so neither multiple-choice rung can be rendered.
        bool CanBuild(ExerciseType t) =>
            t is not (ExerciseType.WordToMeaningChoice or ExerciseType.MeaningToWordChoice);

        Ladder().SelectProbeRung(Card(rung: 2), "noun", Now, CanBuild).Should().Be(0);
    }

    /// <summary>
    /// A copy of the built-in ladder that a test can adjust. The bound property is empty by
    /// default so configuration replaces it rather than being appended to it.
    /// </summary>
    private static StudyOptions OptionsWithLadder() => new()
    {
        Ladder = StudyDefaults.Ladder.Select(rung => new LadderRungOptions { Type = rung.Type }).ToList()
    };

    [Test]
    public void ARungIsWithheldUntilTheCardReachesItsMinimumInterval()
    {
        var options = OptionsWithLadder();
        options.Ladder[3].MinIntervalDays = 10;

        var card = Card(rung: 3, interval: 3);

        Ladder(options).SelectProbeRung(card, "noun", Now, Anything).Should().Be(2);
    }

    [Test]
    public void ARungRestrictedByPartOfSpeechIsSkippedForOtherWords()
    {
        var options = OptionsWithLadder();
        options.Ladder[3].PartsOfSpeech = new List<string> { "verb" };

        Ladder(options).SelectProbeRung(Card(rung: 3), "noun", Now, Anything).Should().Be(2);
        Ladder(options).SelectProbeRung(Card(rung: 3), "transitive verb", Now, Anything).Should().Be(3);
    }

    [Test]
    public void AFailedWordReClimbsTheLadderOverFollowingReviews()
    {
        var ladder = Ladder();

        // Fails cloze, then answers the easier rungs correctly on the days after.
        var rung = ladder.NextRung(3, ReviewGrade.Again, 1.0);
        ladder.TypeAt(rung).Should().Be(ExerciseType.WordToMeaningChoice);

        rung = ladder.NextRung(rung, ReviewGrade.Good, 1.0);
        ladder.TypeAt(rung).Should().Be(ExerciseType.MeaningToWordChoice);

        rung = ladder.NextRung(rung, ReviewGrade.Good, 1.0);
        ladder.TypeAt(rung).Should().Be(ExerciseType.ContextToWordRecall);
    }
}
