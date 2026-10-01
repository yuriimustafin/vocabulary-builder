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
    /// <param name="before">Where the card stood on the ladder when the answer was given.</param>
    /// <param name="after">Where the answer left it.</param>
    /// <param name="phaseRetrievals">Graded tries in this learning phase, this one included.</param>
    /// <param name="previousReviewAtUtc">When the card was last answered before this.</param>
    /// <param name="nowUtc">Now.</param>
    bool IsMet(
        CardState stateBefore,
        ReviewGrade grade,
        RungMove before,
        RungMove after,
        int phaseRetrievals,
        DateTime? previousReviewAtUtc,
        DateTime nowUtc);
}

/// <summary>
/// Learning ends on a criterion rather than after a set number of steps.
///
/// A new word leaves once it has reached the scaffolded level and been built there cleanly
/// - picked from options is not enough, built from its pieces is. It keeps its level, so the
/// climb to production carries on over the following days' reviews rather than all in the
/// first session. A relearning word, which was known once, leaves on its first clean success
/// at whatever level the lapse left it. A word that keeps slipping gets more practice without
/// anyone having to decide it is difficult.
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
        RungMove before,
        RungMove after,
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

        // Clean: it moved the word on. A success with the hint holds it where it was, and
        // does not count; a promotion starts the next level's streak at nought, and does.
        var promoted = after.Rung > before.Rung;
        var clean = promoted || after.Streak > before.Streak;

        if (!clean)
        {
            return false;
        }

        if (stateBefore == CardState.Relearning)
        {
            return promoted || after.Streak >= Math.Max(1, _options.RelearningExitSuccesses);
        }

        var exitLevel = Math.Clamp(_options.LearningExitLevel, 0, _ladder.TopRung);
        var required = Math.Max(1, _options.LearningExitSuccesses);

        // Past the exit level already - promoted out of it - or enough on it
        var enough = after.Rung > exitLevel || (after.Rung == exitLevel && after.Streak >= required);

        if (!enough)
        {
            return false;
        }

        // Two successes seconds apart show the word is still in working memory. When more
        // than one is asked for, the last has to come after a real gap to count.
        return required < 2
            || previousReviewAtUtc is not { } previous
            || nowUtc - previous >= TimeSpan.FromMinutes(_options.LearningExitSpacingMinutes);
    }
}
