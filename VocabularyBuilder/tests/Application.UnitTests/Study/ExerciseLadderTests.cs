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

    private const int Introduction = 0;
    private const int Recognition = 1;
    private const int Scaffolded = 2;
    private const int Production = 3;

    private static ConfiguredExerciseLadder Ladder(StudyOptions? options = null) => new(options ?? new StudyOptions());

    private static ReviewCard Card(
        int rung = 0, int streak = 0, ExerciseType? last = null, int interval = 0, DateTime? lastReviewed = null) => new()
        {
            WordId = 1,
            CurrentRung = rung,
            RungStreak = streak,
            LastExerciseType = last,
            IntervalDays = interval,
            LastReviewedAtUtc = lastReviewed
        };

    private static bool Anything(ExerciseType _) => true;

    private static ExerciseType Probe(ReviewCard card, Func<ExerciseType, bool>? canBuild = null) =>
        Ladder().SelectProbe(card, "noun", Now, canBuild ?? Anything).Type;

    [Test]
    public void TheDefaultLadderHasFourLevelsEachWithItsPool()
    {
        var ladder = Ladder();

        ladder.RungCount.Should().Be(4);
        ladder.TopRung.Should().Be(Production);
        ladder.TypesAt(Introduction).Should().Equal(ExerciseType.WordToMeaningReveal);
        ladder.TypesAt(Recognition).Should().Equal(
            ExerciseType.MeaningToWordChoice, ExerciseType.ContextToWordChoice, ExerciseType.WordToMeaningChoice);
        ladder.TypesAt(Scaffolded).Should().Equal(
            ExerciseType.MeaningToWordSyllableScramble, ExerciseType.MeaningToWordScramble, ExerciseType.MeaningToWordCuedType);
        ladder.TypesAt(Production).Should().Equal(
            ExerciseType.ContextToWordRecall, ExerciseType.MeaningToWordType, ExerciseType.MeaningToWordRecall);
    }

    // --- moving between levels ---------------------------------------------

    [Test]
    public void OneCleanSuccessMovesAWordOutOfRecognition()
    {
        var move = Ladder().NextRung(Card(rung: Recognition), ReviewGrade.Good, hintUsed: false);

        move.Should().Be(new RungMove(Scaffolded, 0), "the next level starts with a clean slate");
    }

    [Test]
    public void AWordStaysOnTheScaffoldedLevelUntilItHasThreeCleanSuccesses()
    {
        var ladder = Ladder();

        ladder.NextRung(Card(rung: Scaffolded, streak: 0), ReviewGrade.Good, false).Should().Be(new RungMove(Scaffolded, 1));
        ladder.NextRung(Card(rung: Scaffolded, streak: 1), ReviewGrade.Easy, false).Should().Be(new RungMove(Scaffolded, 2));
        ladder.NextRung(Card(rung: Scaffolded, streak: 2), ReviewGrade.Good, false).Should().Be(new RungMove(Production, 0));
    }

    [Test]
    public void TheTopLevelIsNeverLeftUpwardsAndItsStreakKeepsCounting()
    {
        Ladder().NextRung(Card(rung: Production, streak: 4), ReviewGrade.Good, false)
            .Should().Be(new RungMove(Production, 5));
    }

    [Test]
    public void AMissDropsOneLevelAndClearsTheStreak()
    {
        Ladder().NextRung(Card(rung: Production, streak: 3), ReviewGrade.Again, false)
            .Should().Be(new RungMove(Scaffolded, 0));
    }

    [Test]
    public void AMissAtTheBottomStaysAtTheBottom()
    {
        Ladder().NextRung(Card(rung: Introduction), ReviewGrade.Again, false).Should().Be(new RungMove(Introduction, 0));
    }

    [Test]
    public void HardHoldsBothTheLevelAndTheStreak()
    {
        // Recalled, but only just: neither a step towards moving up nor a reason to drop.
        Ladder().NextRung(Card(rung: Scaffolded, streak: 2), ReviewGrade.Hard, false)
            .Should().Be(new RungMove(Scaffolded, 2));
    }

    [Test]
    public void ASuccessThatNeededTheHintDoesNotCountTowardsMovingUp()
    {
        Ladder().NextRung(Card(rung: Scaffolded, streak: 2), ReviewGrade.Good, hintUsed: true)
            .Should().Be(new RungMove(Scaffolded, 2));
    }

    [Test]
    public void AWordDroppedToTheFlashcardIsGradedBackUpByOneSuccess()
    {
        // The introduction is met rather than graded the first time, but a word that falls
        // this far is asked the flashcard for real and must be able to leave it.
        Ladder().NextRung(Card(rung: Introduction), ReviewGrade.Good, false).Should().Be(new RungMove(Recognition, 0));
    }

    [Test]
    public void TheDropIsConfigurable()
    {
        var options = new StudyOptions { FailureRungDrop = 2 };

        Ladder(options).NextRung(Card(rung: Production), ReviewGrade.Again, false).Rung.Should().Be(Recognition);
    }

    // --- choosing the exercise ---------------------------------------------

    [Test]
    public void TheSupportFadesAsTheStreakOnALevelGrows()
    {
        // Syllables, then letters, then typing from the first letters.
        Probe(Card(rung: Scaffolded, streak: 0)).Should().Be(ExerciseType.MeaningToWordSyllableScramble);
        Probe(Card(rung: Scaffolded, streak: 1)).Should().Be(ExerciseType.MeaningToWordScramble);
        Probe(Card(rung: Scaffolded, streak: 2)).Should().Be(ExerciseType.MeaningToWordCuedType);
    }

    [Test]
    public void TheTopLevelTakesItsExercisesInTurn()
    {
        Probe(Card(rung: Production, streak: 0)).Should().Be(ExerciseType.ContextToWordRecall);
        Probe(Card(rung: Production, streak: 1)).Should().Be(ExerciseType.MeaningToWordType);
        Probe(Card(rung: Production, streak: 2)).Should().Be(ExerciseType.MeaningToWordRecall);
        Probe(Card(rung: Production, streak: 3)).Should().Be(ExerciseType.ContextToWordRecall);
    }

    [Test]
    public void TheSameExerciseIsNeverAskedTwiceRunningWhenTheLevelHasAnother()
    {
        // After a miss the streak is back to zero, which would ask the same first exercise
        // again; the next one along is asked instead.
        Probe(Card(rung: Recognition, streak: 0, last: ExerciseType.MeaningToWordChoice))
            .Should().Be(ExerciseType.ContextToWordChoice);
        Probe(Card(rung: Production, streak: 2, last: ExerciseType.MeaningToWordRecall))
            .Should().Be(ExerciseType.ContextToWordRecall);
    }

    [Test]
    public void ALevelWithOneExerciseRepeatsIt()
    {
        Probe(Card(rung: Introduction, last: ExerciseType.WordToMeaningReveal))
            .Should().Be(ExerciseType.WordToMeaningReveal);
    }

    [Test]
    public void AnExerciseThatCannotBeBuiltIsSkippedWithinTheLevel()
    {
        // A two-syllable word makes no syllable puzzle, so the letters come first instead.
        bool CanBuild(ExerciseType t) => t != ExerciseType.MeaningToWordSyllableScramble;

        Probe(Card(rung: Scaffolded, streak: 0), CanBuild).Should().Be(ExerciseType.MeaningToWordScramble);
        Probe(Card(rung: Scaffolded, streak: 1), CanBuild).Should().Be(ExerciseType.MeaningToWordCuedType);
    }

    [Test]
    public void ALevelWithNothingBuildableFallsBackToTheNearestOneBelow()
    {
        // No distractor pool: nothing in recognition can be rendered.
        bool CanBuild(ExerciseType t) =>
            t is not (ExerciseType.WordToMeaningChoice or ExerciseType.MeaningToWordChoice or ExerciseType.ContextToWordChoice);

        var probe = Ladder().SelectProbe(Card(rung: Recognition, streak: 0), "noun", Now, CanBuild);

        probe.Rung.Should().Be(Introduction);
        probe.Type.Should().Be(ExerciseType.WordToMeaningReveal);
    }

    [Test]
    public void TheCueLevelIsTheStreakOnTheLevelTheExerciseCameFrom()
    {
        var ladder = Ladder();

        ladder.SelectProbe(Card(rung: Scaffolded, streak: 2), "noun", Now, Anything).CueLevel.Should().Be(2);

        // Fallen back to a level it is not on: it starts that one afresh.
        bool NoScaffold(ExerciseType t) => !ladder.TypesAt(Scaffolded).Contains(t);
        ladder.SelectProbe(Card(rung: Scaffolded, streak: 2), "noun", Now, NoScaffold).CueLevel.Should().Be(0);
    }

    // --- long gaps -----------------------------------------------------------

    [Test]
    public void ALongAbsenceEscalatesToUnhintedTyping()
    {
        // Eight days away from a three-day card: probe hard, with no hint, so the grade
        // measures memory rather than the cue.
        var probe = Ladder().SelectProbe(
            Card(rung: Production, interval: 3, lastReviewed: Now.AddDays(-8)), "noun", Now, Anything);

        probe.Type.Should().Be(ExerciseType.MeaningToWordType);
        probe.Escalated.Should().BeTrue();
    }

    [Test]
    public void BeingWellOverdueEscalatesEvenWhenTheGapIsShort()
    {
        // Two days into a one-day interval is twice as long as intended.
        var probe = Ladder().SelectProbe(
            Card(rung: Production, interval: 1, lastReviewed: Now.AddDays(-2)), "noun", Now, Anything);

        probe.Escalated.Should().BeTrue();
    }

    [Test]
    public void ALongAbsenceDoesNotEscalateAWordStillBelowProduction()
    {
        var probe = Ladder().SelectProbe(
            Card(rung: Scaffolded, interval: 1, lastReviewed: Now.AddDays(-30)), "noun", Now, Anything);

        probe.Escalated.Should().BeFalse();
        probe.Rung.Should().Be(Scaffolded);
    }

    [Test]
    public void AWordSeenOnScheduleIsNotEscalated()
    {
        var probe = Ladder().SelectProbe(
            Card(rung: Production, interval: 5, lastReviewed: Now.AddDays(-5)), "noun", Now, Anything);

        probe.Escalated.Should().BeFalse();
    }

    // --- per-exercise restrictions -------------------------------------------

    /// <summary>
    /// A copy of the built-in ladder that a test can adjust. The bound property is empty by
    /// default so configuration replaces it rather than being appended to it.
    /// </summary>
    private static StudyOptions OptionsWithLadder() => new()
    {
        Ladder = StudyDefaults.Ladder.Select(level => new LadderRungOptions
        {
            Name = level.Name,
            PromoteAfter = level.PromoteAfter,
            Exercises = level.Exercises.Select(e => new LadderExerciseOptions { Type = e.Type }).ToList()
        }).ToList()
    };

    [Test]
    public void AnExerciseIsWithheldUntilTheCardReachesItsMinimumInterval()
    {
        var options = OptionsWithLadder();
        options.Ladder[Production].Exercises[0].MinIntervalDays = 10;

        Ladder(options).SelectProbe(Card(rung: Production, interval: 3), "noun", Now, Anything).Type
            .Should().Be(ExerciseType.MeaningToWordType);
        Ladder(options).SelectProbe(Card(rung: Production, interval: 12), "noun", Now, Anything).Type
            .Should().Be(ExerciseType.ContextToWordRecall);
    }

    [Test]
    public void AnExerciseRestrictedByPartOfSpeechIsSkippedForOtherWords()
    {
        var options = OptionsWithLadder();
        options.Ladder[Production].Exercises[0].PartsOfSpeech = new List<string> { "verb" };

        Ladder(options).SelectProbe(Card(rung: Production), "noun", Now, Anything).Type
            .Should().Be(ExerciseType.MeaningToWordType);
        Ladder(options).SelectProbe(Card(rung: Production), "transitive verb", Now, Anything).Type
            .Should().Be(ExerciseType.ContextToWordRecall);
    }

    // --- the whole climb ------------------------------------------------------

    [Test]
    public void AWordAnsweredCleanlyMeetsEachExerciseOnItsWayUpAndThenProduction()
    {
        var ladder = Ladder();
        var card = Card(rung: Recognition);
        var seen = new List<ExerciseType>();

        for (var i = 0; i < 6; i++)
        {
            var probe = ladder.SelectProbe(card, "noun", Now, Anything);
            seen.Add(probe.Type);

            var move = ladder.NextRung(card, ReviewGrade.Good, false);
            card.CurrentRung = move.Rung;
            card.RungStreak = move.Streak;
            card.LastExerciseType = probe.Type;
        }

        seen.Should().Equal(
            ExerciseType.MeaningToWordChoice,
            ExerciseType.MeaningToWordSyllableScramble,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.MeaningToWordCuedType,
            ExerciseType.ContextToWordRecall,
            ExerciseType.MeaningToWordType);
    }

    [Test]
    public void AMissedWordMeetsTheLevelBelowAndClimbsBack()
    {
        var ladder = Ladder();
        var card = Card(rung: Production, streak: 1, last: ExerciseType.MeaningToWordType);

        var move = ladder.NextRung(card, ReviewGrade.Again, false);
        card.CurrentRung = move.Rung;
        card.RungStreak = move.Streak;

        ladder.SelectProbe(card, "noun", Now, Anything).Type.Should().Be(ExerciseType.MeaningToWordSyllableScramble);

        for (var i = 0; i < 3; i++)
        {
            move = ladder.NextRung(card, ReviewGrade.Good, false);
            card.CurrentRung = move.Rung;
            card.RungStreak = move.Streak;
        }

        card.CurrentRung.Should().Be(Production);
    }
}
