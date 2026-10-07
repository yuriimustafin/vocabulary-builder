using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Scheduling;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class LearningExitCriterionTests
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    private const int Recognition = 1;
    private const int Scaffolded = 2;
    private const int Production = 3;

    private static LearningExitCriterion Criterion(StudyOptions? options = null)
    {
        options ??= new StudyOptions();
        return new LearningExitCriterion(options, new ConfiguredExerciseLadder(options));
    }

    private static bool IsMet(
        RungMove before,
        RungMove after,
        CardState state = CardState.Learning,
        ReviewGrade grade = ReviewGrade.Good,
        int retrievals = 5,
        double minutesSincePrevious = 6,
        StudyOptions? options = null) =>
        Criterion(options).IsMet(state, grade, before, after, retrievals, Now.AddMinutes(-minutesSincePrevious), Now);

    [Test]
    public void ANewWordLeavesOnceItHasBeenBuiltCleanlyOnTheScaffoldedLevel()
    {
        // Built once from its pieces: enough for today. The climb to production carries on
        // over the following days' reviews, not all in the first session.
        IsMet(new RungMove(Scaffolded, 0), new RungMove(Scaffolded, 1)).Should().BeTrue();
    }

    [Test]
    public void RecognisingAWordIsNotEnoughToLeave()
    {
        IsMet(new RungMove(Recognition, 1), new RungMove(Recognition, 2)).Should().BeFalse();
    }

    [Test]
    public void ArrivingOnTheScaffoldedLevelIsNotYetBuildingTheWord()
    {
        // Promoted out of recognition: the streak there starts at nought, and nothing has
        // been built yet
        IsMet(new RungMove(Recognition, 2), new RungMove(Scaffolded, 0)).Should().BeFalse();
    }

    [Test]
    public void AWordAlreadyPastTheExitLevelLeavesOnItsNextCleanSuccess()
    {
        // Promoted from scaffolded to production, or answering there - either way it is past
        IsMet(new RungMove(Scaffolded, 2), new RungMove(Production, 0)).Should().BeTrue();
        IsMet(new RungMove(Production, 0), new RungMove(Production, 1)).Should().BeTrue();
    }

    [Test]
    public void ASuccessWithTheHintHoldsTheWordAndDoesNotCount()
    {
        IsMet(new RungMove(Scaffolded, 1), new RungMove(Scaffolded, 1)).Should().BeFalse();
    }

    [Test]
    public void ARelearningWordLeavesOnItsFirstCleanSuccessWhereverTheLapseLeftIt()
    {
        // A lapse on production drops the word a level; it does not have to climb back first
        IsMet(new RungMove(Recognition, 0), new RungMove(Recognition, 1), CardState.Relearning, minutesSincePrevious: 0.5)
            .Should().BeTrue();
        IsMet(new RungMove(Recognition, 2), new RungMove(Scaffolded, 0), CardState.Relearning)
            .Should().BeTrue("a promotion is a clean success too");
        IsMet(new RungMove(Recognition, 1), new RungMove(Recognition, 1), CardState.Relearning)
            .Should().BeFalse("held with the hint, it was not clean");
    }

    [Test]
    public void OnlyASuccessCanEndLearning()
    {
        IsMet(new RungMove(Scaffolded, 0), new RungMove(Scaffolded, 0), grade: ReviewGrade.Hard).Should().BeFalse();
        IsMet(new RungMove(Scaffolded, 0), new RungMove(Recognition, 0), grade: ReviewGrade.Again, retrievals: 50)
            .Should().BeFalse();
    }

    [Test]
    public void AStubbornWordLeavesOnItsNextSuccessOnceItHasHadItsShare()
    {
        IsMet(new RungMove(Recognition, 0), new RungMove(Recognition, 1), retrievals: 9).Should().BeFalse();
        IsMet(new RungMove(Recognition, 0), new RungMove(Recognition, 1), retrievals: 10).Should().BeTrue();
    }

    [Test]
    public void ACardInReviewIsNotLearning()
    {
        IsMet(new RungMove(Production, 4), new RungMove(Production, 5), CardState.Review).Should().BeFalse();
    }

    [Test]
    public void TheLevelAndTheCountAreConfigurable()
    {
        // The previous rule: two clean successes on production, a real gap apart
        var options = new StudyOptions { LearningExitLevel = 3, LearningExitSuccesses = 2, LearningExitSpacingMinutes = 4 };

        IsMet(new RungMove(Scaffolded, 0), new RungMove(Scaffolded, 1), options: options).Should().BeFalse();
        IsMet(new RungMove(Production, 0), new RungMove(Production, 1), options: options).Should().BeFalse();
        IsMet(new RungMove(Production, 1), new RungMove(Production, 2), minutesSincePrevious: 1, options: options)
            .Should().BeFalse("two answers seconds apart show working memory, not learning");
        IsMet(new RungMove(Production, 1), new RungMove(Production, 2), minutesSincePrevious: 5, options: options)
            .Should().BeTrue();
    }
}
