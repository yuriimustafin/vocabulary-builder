using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Scheduling;

public interface ILearningExitCriterion
{
    /// <summary>
    /// Whether a word being learned has done enough today to leave for a day-scale interval.
    /// </summary>
    /// <param name="stateBefore">The card's state when the answer was given.</param>
    /// <param name="grade">The grade the answer earned.</param>
    /// <param name="move">Where the answer left the card on the ladder.</param>
    /// <param name="phaseRetrievals">Graded tries in this learning phase, this one included.</param>
    /// <param name="previousReviewAtUtc">When the card was last answered before this.</param>
    /// <param name="nowUtc">Now.</param>
    bool IsMet(
        CardState stateBefore,
        ReviewGrade grade,
        RungMove move,
        int phaseRetrievals,
        DateTime? previousReviewAtUtc,
        DateTime nowUtc);
}

/// <summary>
/// Learning ends on a criterion rather than after a set number of steps.
///
/// A new word leaves once it has been produced cleanly on the top level twice, a few
/// minutes apart; a relearning word, which was known once, after one. A word that keeps
/// slipping gets more practice without anyone having to decide it is difficult, and a word
/// that is already known leaves as soon as it has shown it - by producing it, never by
/// answering quickly.
/// </summary>
public class LearningExitCriterion : ILearningExitCriterion
{
    private readonly StudyOptions _options;
    private readonly IExerciseLadder _ladder;

    public LearningExitCriterion(StudyOptions options, IExerciseLadder ladder)
    {
        _options = options;
        _ladder = ladder;
    }

    public bool IsMet(
        CardState stateBefore,
        ReviewGrade grade,
        RungMove move,
        int phaseRetrievals,
        DateTime? previousReviewAtUtc,
        DateTime nowUtc)
    {
        if (stateBefore is not (CardState.New or CardState.Learning or CardState.Relearning)
            || grade < ReviewGrade.Good)
        {
            return false;
        }

        // A stubborn word goes on its next success whatever else is true. It comes back
        // tomorrow either way, and the rest of the session is not held up for it.
        if (phaseRetrievals >= _options.MaxLearningRetrievals)
        {
            return true;
        }

        var required = stateBefore == CardState.Relearning
            ? _options.RelearningExitSuccesses
            : _options.LearningExitSuccesses;

        if (move.Rung < _ladder.TopRung || move.Streak < Math.Max(1, required))
        {
            return false;
        }

        // Two successes seconds apart show the word is still in working memory. The last
        // one has to come after a real gap to count as having learned it.
        return required < 2
            || previousReviewAtUtc is not { } previous
            || nowUtc - previous >= TimeSpan.FromMinutes(_options.LearningExitSpacingMinutes);
    }
}
