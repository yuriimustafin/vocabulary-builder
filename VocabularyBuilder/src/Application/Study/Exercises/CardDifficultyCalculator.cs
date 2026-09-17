using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

public interface ICardDifficultyCalculator
{
    CardDifficulty Calculate(ReviewCard card);

    /// <summary>Folds one graded result into the card's moving success average.</summary>
    double NextSuccessRate(double current, ReviewGrade grade);
}

/// <summary>
/// Turns a card's ease, recent failures and moving success rate into a coarse tier.
/// The tier decides how much re-encoding support a word gets after its graded probe.
/// </summary>
public class CardDifficultyCalculator : ICardDifficultyCalculator
{
    private readonly StudyOptions _options;

    public CardDifficultyCalculator(StudyOptions options) => _options = options;

    public CardDifficulty Calculate(ReviewCard card)
    {
        var tiers = _options.DifficultyTiers;

        // Any one bad signal is enough to call a word difficult.
        if (card.EaseFactor < tiers.DifficultMaxEase
            || card.LapsesSinceRecovery >= tiers.DifficultMinLapses
            || card.RecentSuccessRate < tiers.DifficultMaxSuccess)
        {
            return CardDifficulty.Difficult;
        }

        // Comfortable needs every signal to be good.
        if (card.EaseFactor >= tiers.ComfortableMinEase
            && card.LapsesSinceRecovery == 0
            && card.RecentSuccessRate >= tiers.ComfortableMinSuccess)
        {
            return CardDifficulty.Comfortable;
        }

        return CardDifficulty.Shaky;
    }

    public double NextSuccessRate(double current, ReviewGrade grade)
    {
        var success = grade >= ReviewGrade.Good ? 1.0 : 0.0;
        var alpha = Math.Clamp(_options.SuccessEmaAlpha, 0.0, 1.0);
        return Math.Clamp(current + alpha * (success - current), 0.0, 1.0);
    }
}
