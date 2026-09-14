using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class GradeResolverTests
{
    private static GradeResolver Resolver() => new(new StudyOptions());

    [TestCase(ExerciseType.WordToMeaningChoice)]
    [TestCase(ExerciseType.MeaningToWordChoice)]
    [TestCase(ExerciseType.MeaningToWordScramble)]
    public void AWrongAnswerIsAlwaysAgain(ExerciseType type)
    {
        Resolver().Resolve(type, new AutoGradeSignals(Correct: false, ElapsedMs: 500))
            .Should().Be(ReviewGrade.Again);
    }

    [TestCase(ExerciseType.WordToMeaningChoice)]
    [TestCase(ExerciseType.MeaningToWordScramble)]
    public void GivingUpIsAlwaysAgain(ExerciseType type)
    {
        Resolver().Resolve(type, new AutoGradeSignals(Correct: true, ElapsedMs: 500, Abandoned: true))
            .Should().Be(ReviewGrade.Again);
    }

    [Test]
    public void MultipleChoiceIsGradedOnSpeed()
    {
        var resolver = Resolver();

        resolver.Resolve(ExerciseType.WordToMeaningChoice, new AutoGradeSignals(true, 1_500))
            .Should().Be(ReviewGrade.Easy);
        resolver.Resolve(ExerciseType.WordToMeaningChoice, new AutoGradeSignals(true, 6_000))
            .Should().Be(ReviewGrade.Good);
        resolver.Resolve(ExerciseType.WordToMeaningChoice, new AutoGradeSignals(true, 12_000))
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void AssemblingTheWordCleanlyAndQuicklyIsEasy()
    {
        Resolver().Resolve(ExerciseType.MeaningToWordScramble, new AutoGradeSignals(true, 2_000))
            .Should().Be(ReviewGrade.Easy);
    }

    [Test]
    public void StartingTheSpellingOverIsHardHoweverFastItFinished()
    {
        // Needing to restart means the spelling was not actually known.
        Resolver().Resolve(ExerciseType.MeaningToWordScramble, new AutoGradeSignals(true, 1_000, Resets: 1))
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void ResetsOnlyPenaliseTheExerciseThatCanHaveThem()
    {
        Resolver().Resolve(ExerciseType.WordToMeaningChoice, new AutoGradeSignals(true, 1_000, Resets: 3))
            .Should().Be(ReviewGrade.Easy);
    }

    [Test]
    public void ThresholdsComeFromConfiguration()
    {
        var options = new StudyOptions { FastAnswerMs = 500, SlowAnswerMs = 2_000 };
        var resolver = new GradeResolver(options);

        resolver.Resolve(ExerciseType.WordToMeaningChoice, new AutoGradeSignals(true, 1_500))
            .Should().Be(ReviewGrade.Good);
        resolver.Resolve(ExerciseType.WordToMeaningChoice, new AutoGradeSignals(true, 2_500))
            .Should().Be(ReviewGrade.Hard);
    }
}
