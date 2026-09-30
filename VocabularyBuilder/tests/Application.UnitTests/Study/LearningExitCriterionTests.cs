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

    private const int Top = 3;

    private static LearningExitCriterion Criterion(StudyOptions? options = null)
    {
        options ??= new StudyOptions();
        return new LearningExitCriterion(options, new ConfiguredExerciseLadder(options));
    }

    private static bool IsMet(
        RungMove move,
        CardState state = CardState.Learning,
        ReviewGrade grade = ReviewGrade.Good,
        int retrievals = 5,
        double minutesSincePrevious = 6) =>
        Criterion().IsMet(state, grade, move, retrievals, Now.AddMinutes(-minutesSincePrevious), Now);

    [Test]
    public void ANewWordLeavesAfterTwoCleanSuccessesOnTheTopLevel()
    {
        IsMet(new RungMove(Top, 1)).Should().BeFalse();
        IsMet(new RungMove(Top, 2)).Should().BeTrue();
    }

    [Test]
    public void SuccessesBelowTheTopLevelNeverCount()
    {
        IsMet(new RungMove(Top - 1, 5)).Should().BeFalse();
    }

    [Test]
    public void TheLastSuccessHasToComeAfterARealGap()
    {
        // Two answers seconds apart show the word is in working memory, not that it is learned.
        IsMet(new RungMove(Top, 2), minutesSincePrevious: 1).Should().BeFalse();
        IsMet(new RungMove(Top, 3), minutesSincePrevious: 4).Should().BeTrue();
    }

    [Test]
    public void ARelearningWordNeedsOnlyOne()
    {
        IsMet(new RungMove(Top, 1), CardState.Relearning, minutesSincePrevious: 0.5).Should().BeTrue();
    }

    [Test]
    public void OnlyASuccessCanEndLearning()
    {
        IsMet(new RungMove(Top, 2), grade: ReviewGrade.Hard).Should().BeFalse();
        IsMet(new RungMove(Top, 0), grade: ReviewGrade.Again, retrievals: 50).Should().BeFalse();
    }

    [Test]
    public void AStubbornWordLeavesOnItsNextSuccessOnceItHasHadItsShare()
    {
        IsMet(new RungMove(1, 0), retrievals: 9).Should().BeFalse();
        IsMet(new RungMove(1, 0), retrievals: 10).Should().BeTrue();
    }

    [Test]
    public void ACardInReviewIsNotLearning()
    {
        IsMet(new RungMove(Top, 5), CardState.Review).Should().BeFalse();
    }

    [Test]
    public void TheRequirementsAreConfigurable()
    {
        var options = new StudyOptions { LearningExitSuccesses = 3, LearningExitSpacingMinutes = 10 };

        Criterion(options).IsMet(CardState.Learning, ReviewGrade.Good, new RungMove(Top, 2), 5, Now.AddMinutes(-20), Now)
            .Should().BeFalse();
        Criterion(options).IsMet(CardState.Learning, ReviewGrade.Good, new RungMove(Top, 3), 5, Now.AddMinutes(-5), Now)
            .Should().BeFalse();
        Criterion(options).IsMet(CardState.Learning, ReviewGrade.Good, new RungMove(Top, 3), 5, Now.AddMinutes(-10), Now)
            .Should().BeTrue();
    }
}
