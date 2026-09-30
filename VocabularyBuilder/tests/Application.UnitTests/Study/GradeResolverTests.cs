using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class GradeResolverTests
{
    private static GradeResolver Resolver() => new(new StudyOptions());

    [TestCase(AnswerKind.Recognised)]
    [TestCase(AnswerKind.Built)]
    public void AWrongAnswerIsAlwaysAgain(AnswerKind kind)
    {
        Resolver().Resolve(kind, new AutoGradeSignals(Correct: false, ElapsedMs: 500))
            .Should().Be(ReviewGrade.Again);
    }

    [TestCase(AnswerKind.Recognised)]
    [TestCase(AnswerKind.Built)]
    public void GivingUpIsAlwaysAgain(AnswerKind kind)
    {
        Resolver().Resolve(kind, new AutoGradeSignals(Correct: true, ElapsedMs: 500, Abandoned: true))
            .Should().Be(ReviewGrade.Again);
    }

    [Test]
    public void MultipleChoiceIsGradedOnSpeed()
    {
        var resolver = Resolver();

        resolver.Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 1_500))
            .Should().Be(ReviewGrade.Good);
        resolver.Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 6_000))
            .Should().Be(ReviewGrade.Good);
        resolver.Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 12_000))
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void RecognisingAWordIsNeverEasyHoweverFast()
    {
        // A quick pick from options may be elimination or familiarity, not recall.
        Resolver().Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 300))
            .Should().Be(ReviewGrade.Good);
    }

    [Test]
    public void AssemblingTheWordCleanlyAndQuicklyIsEasy()
    {
        Resolver().Resolve(AnswerKind.Built, new AutoGradeSignals(true, 2_000))
            .Should().Be(ReviewGrade.Easy);
    }

    [Test]
    public void StartingTheSpellingOverIsHardHoweverFastItFinished()
    {
        // Needing to restart means the spelling was not actually known.
        Resolver().Resolve(AnswerKind.Built, new AutoGradeSignals(true, 1_000, Resets: 1))
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void ResetsOnlyPenaliseTheExerciseThatCanHaveThem()
    {
        Resolver().Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 6_000, Resets: 3))
            .Should().Be(ReviewGrade.Good);
    }

    [Test]
    public void ThresholdsComeFromConfiguration()
    {
        var options = new StudyOptions { FastAnswerMs = 500, SlowAnswerMs = 2_000 };
        var resolver = new GradeResolver(options);

        resolver.Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 1_500))
            .Should().Be(ReviewGrade.Good);
        resolver.Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 2_500))
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void PickingTheMissingWordForASentenceIsRecognitionToo()
    {
        Resolver().Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 500))
            .Should().Be(ReviewGrade.Good);
    }

    [Test]
    public void BuildingALongWordIsAllowedTimeInProportionToItsLength()
    {
        var resolver = Resolver();

        // Fourteen letters at 1.5 seconds each: fifteen seconds is fair, not slow.
        resolver.Resolve(AnswerKind.Built, new AutoGradeSignals(true, 15_000, Length: 14)).Should().Be(ReviewGrade.Good);
        resolver.Resolve(AnswerKind.Built, new AutoGradeSignals(true, 22_000, Length: 14)).Should().Be(ReviewGrade.Hard);

        // A short word still has the ordinary limit.
        resolver.Resolve(AnswerKind.Built, new AutoGradeSignals(true, 11_000, Length: 4)).Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void AClickIsHeldToTheOrdinaryLimitWhateverTheWordsLength()
    {
        Resolver().Resolve(AnswerKind.Recognised, new AutoGradeSignals(true, 12_000, Length: 14))
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void StartingTheSyllablesOverIsHardToo()
    {
        Resolver().Resolve(AnswerKind.Built, new AutoGradeSignals(true, 1_000, Resets: 1))
            .Should().Be(ReviewGrade.Hard);
    }
}
