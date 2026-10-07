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
            ExerciseType.WordToSpellingCopy, ExerciseType.MeaningToWordChoice,
            ExerciseType.ContextToWordChoice, ExerciseType.WordToMeaningChoice);
        ladder.TypesAt(Scaffolded).Should().Equal(
            ExerciseType.WordToSpellingCover, ExerciseType.MeaningToWordScramble,
            ExerciseType.TranslationToSentenceScramble, ExerciseType.MeaningToWordCuedType);
        ladder.TypesAt(Production).Should().Equal(ExerciseType.ContextToWordRecall, ExerciseType.MeaningToWordRecall);
    }

    // --- moving between levels ---------------------------------------------

    [Test]
    public void ThreeCleanSuccessesMoveAWordOutOfRecognition()
    {
        var ladder = Ladder();

        ladder.NextRung(Card(rung: Recognition, streak: 1), ReviewGrade.Good, false).Should().Be(new RungMove(Recognition, 2));
        ladder.NextRung(Card(rung: Recognition, streak: 2), ReviewGrade.Good, false)
            .Should().Be(new RungMove(Scaffolded, 0), "the next level starts with a clean slate");
    }

    [Test]
    public void RecognitionCopiesTheWordThenPicksItFromItsMeaningThenFromASentence()
    {
        Probe(Card(rung: Recognition, streak: 0)).Should().Be(ExerciseType.WordToSpellingCopy);
        Probe(Card(rung: Recognition, streak: 1, last: ExerciseType.WordToSpellingCopy))
            .Should().Be(ExerciseType.MeaningToWordChoice);
        Probe(Card(rung: Recognition, streak: 2, last: ExerciseType.MeaningToWordChoice))
            .Should().Be(ExerciseType.ContextToWordChoice);
    }

    [Test]
    public void TheExercisesWhoseMissesCostNothingAreMarkedTolerant()
    {
        var ladder = Ladder();

        ladder.IsTolerant(ExerciseType.WordToSpellingCover).Should().BeTrue("a slip writing from memory costs nothing");
        ladder.IsTolerant(ExerciseType.MeaningToWordScramble).Should().BeTrue("ordering pieces is not knowing the word");
        ladder.IsTolerant(ExerciseType.TranslationToSentenceScramble).Should().BeTrue();
        ladder.IsTolerant(ExerciseType.WordToSpellingCopy).Should().BeFalse();
        ladder.IsTolerant(ExerciseType.WordToCollocatesChoice).Should().BeFalse("it is no longer on the ladder");
        ladder.IsTolerant(ExerciseType.MeaningToWordChoice).Should().BeFalse();
        ladder.IsTolerant(ExerciseType.ContextToWordRecall).Should().BeFalse();
        ladder.IsTolerant(ExerciseType.MeaningToWordType).Should().BeFalse("it is not on the ladder at all");
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
        // Written from memory, then put together from its pieces, then a sentence around it
        Probe(Card(rung: Scaffolded, streak: 0, interval: 3)).Should().Be(ExerciseType.WordToSpellingCover);
        Probe(Card(rung: Scaffolded, streak: 1, interval: 3)).Should().Be(ExerciseType.MeaningToWordScramble);
        Probe(Card(rung: Scaffolded, streak: 2, interval: 3)).Should().Be(ExerciseType.TranslationToSentenceScramble);
    }

    [Test]
    public void NoSentenceIsRebuiltOnTheDayAWordIsFirstLearned()
    {
        // Still learning, so not yet a day out: the sentence waits for the reviews
        Probe(Card(rung: Scaffolded, streak: 2, interval: 0)).Should().Be(ExerciseType.MeaningToWordCuedType);
    }

    [Test]
    public void TheTopLevelTakesItsExercisesInTurn()
    {
        Probe(Card(rung: Production, streak: 0)).Should().Be(ExerciseType.ContextToWordRecall);
        Probe(Card(rung: Production, streak: 1)).Should().Be(ExerciseType.MeaningToWordRecall);
        Probe(Card(rung: Production, streak: 2)).Should().Be(ExerciseType.ContextToWordRecall);
    }

    [Test]
    public void TheSameExerciseIsNeverAskedTwiceRunningWhenTheLevelHasAnother()
    {
        // After a miss the streak is back to zero, which would ask the same first exercise
        // again; the next one along is asked instead.
        Probe(Card(rung: Recognition, streak: 0, last: ExerciseType.WordToSpellingCopy))
            .Should().Be(ExerciseType.MeaningToWordChoice);
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
        // A word with no sentence of its own is asked to type the word instead.
        bool NoSentence(ExerciseType t) => t != ExerciseType.TranslationToSentenceScramble;

        Probe(Card(rung: Scaffolded, streak: 2, interval: 3)).Should().Be(ExerciseType.TranslationToSentenceScramble);
        Probe(Card(rung: Scaffolded, streak: 2, interval: 3), NoSentence).Should().Be(ExerciseType.MeaningToWordCuedType);
    }

    [Test]
    public void ALevelWithNothingBuildableFallsBackToTheNearestOneBelow()
    {
        // No distractor pool: nothing in recognition can be rendered.
        bool CanBuild(ExerciseType t) =>
            t is not (ExerciseType.WordToSpellingCopy or ExerciseType.WordToMeaningChoice
                or ExerciseType.MeaningToWordChoice or ExerciseType.ContextToWordChoice);

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
    public void ALongAbsenceEscalatesToUnhintedRecall()
    {
        // Eight days away from a three-day card: probe hard, with no hint, so the grade
        // measures memory rather than the cue.
        var probe = Ladder().SelectProbe(
            Card(rung: Production, interval: 3, lastReviewed: Now.AddDays(-8)), "noun", Now, Anything);

        probe.Type.Should().Be(ExerciseType.MeaningToWordRecall);
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
            .Should().Be(ExerciseType.MeaningToWordRecall);
        Ladder(options).SelectProbe(Card(rung: Production, interval: 12), "noun", Now, Anything).Type
            .Should().Be(ExerciseType.ContextToWordRecall);
    }

    [Test]
    public void AnExerciseRestrictedByPartOfSpeechIsSkippedForOtherWords()
    {
        var options = OptionsWithLadder();
        options.Ladder[Production].Exercises[0].PartsOfSpeech = new List<string> { "verb" };

        Ladder(options).SelectProbe(Card(rung: Production), "noun", Now, Anything).Type
            .Should().Be(ExerciseType.MeaningToWordRecall);
        Ladder(options).SelectProbe(Card(rung: Production), "transitive verb", Now, Anything).Type
            .Should().Be(ExerciseType.ContextToWordRecall);
    }

    [Test]
    public void ALongAbsenceDoesNotEscalateToAProbeWithheldByItsMinimumInterval()
    {
        var options = OptionsWithLadder();
        options.Ladder[Production].Exercises[1].MinIntervalDays = 7;

        // Eight days away from a three-day card would escalate - but not to an exercise the
        // configuration withholds from a card on a three-day interval
        var probe = Ladder(options).SelectProbe(
            Card(rung: Production, interval: 3, lastReviewed: Now.AddDays(-8)), "noun", Now, Anything);

        probe.Escalated.Should().BeFalse();
        probe.Type.Should().Be(ExerciseType.ContextToWordRecall);
    }

    [Test]
    public void ALongAbsenceDoesNotEscalateToAProbeRestrictedToAnotherPartOfSpeech()
    {
        var options = OptionsWithLadder();
        options.Ladder[Production].Exercises[1].PartsOfSpeech = new List<string> { "verb" };

        var card = Card(rung: Production, interval: 3, lastReviewed: Now.AddDays(-8));

        Ladder(options).SelectProbe(card, "noun", Now, Anything).Escalated.Should().BeFalse();
        Ladder(options).SelectProbe(card, "verb", Now, Anything).Escalated.Should().BeTrue();
    }

    // --- the whole climb ------------------------------------------------------

    [Test]
    public void AWordAnsweredCleanlyMeetsEachExerciseOnItsWayUpAndThenProduction()
    {
        var ladder = Ladder();
        // A day out, as a word is once it has left learning, so the sentence is offered too
        var card = Card(rung: Recognition, interval: 3);
        var seen = new List<ExerciseType>();

        for (var i = 0; i < 8; i++)
        {
            var probe = ladder.SelectProbe(card, "noun", Now, Anything);
            seen.Add(probe.Type);

            var move = ladder.NextRung(card, ReviewGrade.Good, false);
            card.CurrentRung = move.Rung;
            card.RungStreak = move.Streak;
            card.LastExerciseType = probe.Type;
        }

        seen.Should().Equal(
            ExerciseType.WordToSpellingCopy,
            ExerciseType.MeaningToWordChoice,
            ExerciseType.ContextToWordChoice,
            ExerciseType.WordToSpellingCover,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.TranslationToSentenceScramble,
            ExerciseType.ContextToWordRecall,
            ExerciseType.MeaningToWordRecall);
    }

    [Test]
    public void AMissedWordMeetsTheLevelBelowAndClimbsBack()
    {
        var ladder = Ladder();
        var card = Card(rung: Production, streak: 1, last: ExerciseType.MeaningToWordRecall);

        var move = ladder.NextRung(card, ReviewGrade.Again, false);
        card.CurrentRung = move.Rung;
        card.RungStreak = move.Streak;

        ladder.SelectProbe(card, "noun", Now, Anything).Type.Should().Be(ExerciseType.WordToSpellingCover);

        for (var i = 0; i < 3; i++)
        {
            move = ladder.NextRung(card, ReviewGrade.Good, false);
            card.CurrentRung = move.Rung;
            card.RungStreak = move.Streak;
        }

        card.CurrentRung.Should().Be(Production);
    }
}
