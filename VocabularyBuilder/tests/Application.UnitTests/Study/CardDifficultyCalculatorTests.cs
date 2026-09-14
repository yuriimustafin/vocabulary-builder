using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class CardDifficultyCalculatorTests
{
    private static CardDifficultyCalculator Calculator() => new(new StudyOptions());

    private static ReviewCard Card(double ease = 2.5, int lapsesSinceRecovery = 0, double successRate = 1.0) => new()
    {
        WordId = 1,
        EaseFactor = ease,
        LapsesSinceRecovery = lapsesSinceRecovery,
        RecentSuccessRate = successRate
    };

    [Test]
    public void AFreshCardStartsComfortable()
    {
        Calculator().Calculate(Card()).Should().Be(CardDifficulty.Comfortable);
    }

    [Test]
    public void ComfortableNeedsEverySignalToBeGood()
    {
        Calculator().Calculate(Card(ease: 2.3, successRate: 0.85)).Should().Be(CardDifficulty.Comfortable);

        // One lapse since recovery loses it, even with good ease and success behind it.
        Calculator().Calculate(Card(ease: 2.5, lapsesSinceRecovery: 1)).Should().Be(CardDifficulty.Shaky);
    }

    [Test]
    public void AnyOneBadSignalMakesAWordDifficult()
    {
        Calculator().Calculate(Card(ease: 1.7)).Should().Be(CardDifficulty.Difficult);
        Calculator().Calculate(Card(lapsesSinceRecovery: 2)).Should().Be(CardDifficulty.Difficult);
        Calculator().Calculate(Card(successRate: 0.6)).Should().Be(CardDifficulty.Difficult);
    }

    [Test]
    public void TheMiddleGroundIsShaky()
    {
        Calculator().Calculate(Card(ease: 2.0, successRate: 0.8)).Should().Be(CardDifficulty.Shaky);
    }

    [Test]
    public void SuccessesPullTheMovingAverageUpAndFailuresPullItDown()
    {
        var calculator = Calculator();

        calculator.NextSuccessRate(0.5, ReviewGrade.Good).Should().BeApproximately(0.65, 1e-9);
        calculator.NextSuccessRate(0.5, ReviewGrade.Easy).Should().BeApproximately(0.65, 1e-9);
        calculator.NextSuccessRate(0.5, ReviewGrade.Again).Should().BeApproximately(0.35, 1e-9);

        // Hard counts as a miss: the word was not recalled cleanly.
        calculator.NextSuccessRate(0.5, ReviewGrade.Hard).Should().BeApproximately(0.35, 1e-9);
    }

    [Test]
    public void TheMovingAverageStaysWithinZeroAndOne()
    {
        var calculator = Calculator();

        calculator.NextSuccessRate(1.0, ReviewGrade.Good).Should().BeLessThanOrEqualTo(1.0);
        calculator.NextSuccessRate(0.0, ReviewGrade.Again).Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Test]
    public void RepeatedFailuresEventuallyTipAComfortableWordIntoDifficult()
    {
        var calculator = Calculator();
        var rate = 1.0;

        for (var i = 0; i < 4; i++)
        {
            rate = calculator.NextSuccessRate(rate, ReviewGrade.Again);
        }

        calculator.Calculate(Card(successRate: rate)).Should().Be(CardDifficulty.Difficult);
    }
}
