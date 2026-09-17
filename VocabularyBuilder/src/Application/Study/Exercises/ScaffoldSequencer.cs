using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// One ungraded re-encoding exercise served after the graded probe.
/// </summary>
/// <param name="Type">Exercise to render.</param>
/// <param name="RevealedLetters">
/// Letters to show for <see cref="ExerciseType.MeaningToWordPartialLetters"/>; ignored otherwise.
/// </param>
public record ScaffoldStep(ExerciseType Type, int RevealedLetters = 0);

public interface IScaffoldSequencer
{
    IReadOnlyList<ScaffoldStep> Build(int probeRung, ReviewGrade grade, CardDifficulty difficulty, int headwordLength);
}

/// <summary>
/// Builds the follow-up sequence that runs after a graded probe.
///
/// A failure triggers diminishing-cues retrieval practice: the word is re-attempted with
/// progressively more of it revealed until it can be produced. That pattern is documented
/// to help in exactly the situation a plain retry does not - a word that was just missed.
///
/// A merely shaky word gets a gentler version: the rungs just below the probe, replayed
/// as re-exposure. Nothing here is graded, so none of it can inflate the card's ease.
/// </summary>
public class ScaffoldSequencer : IScaffoldSequencer
{
    private readonly StudyOptions _options;
    private readonly IExerciseLadder _ladder;

    public ScaffoldSequencer(StudyOptions options, IExerciseLadder ladder)
    {
        _options = options;
        _ladder = ladder;
    }

    public IReadOnlyList<ScaffoldStep> Build(int probeRung, ReviewGrade grade, CardDifficulty difficulty, int headwordLength)
    {
        if (grade == ReviewGrade.Again)
        {
            return DiminishingCues(headwordLength);
        }

        var count = _options.FollowUpsByTier.For(difficulty);

        // A Hard answer means the word was only just retrieved, so treat it like a
        // difficult card regardless of what the tier says.
        if (grade == ReviewGrade.Hard)
        {
            count = Math.Max(count, _options.FollowUpsByTier.Difficult);
        }

        if (count <= 0 || probeRung <= 0)
        {
            return Array.Empty<ScaffoldStep>();
        }

        var start = Math.Max(0, probeRung - count);
        return Enumerable.Range(start, probeRung - start)
            .Select(rung => new ScaffoldStep(_ladder.TypeAt(rung)))
            .ToList();
    }

    /// <summary>
    /// Cues shrink step by step: a first letter, then roughly half the word, then the
    /// letters shuffled as tiles, and finally the whole word alongside its meaning.
    /// The session stops at whichever step the learner finally produces the word.
    /// </summary>
    private static List<ScaffoldStep> DiminishingCues(int headwordLength)
    {
        var steps = new List<ScaffoldStep>();

        if (headwordLength > 1)
        {
            steps.Add(new ScaffoldStep(ExerciseType.MeaningToWordPartialLetters, 1));

            var partial = (int)Math.Round(headwordLength * 0.4);
            if (partial > 1)
            {
                steps.Add(new ScaffoldStep(ExerciseType.MeaningToWordPartialLetters, partial));
            }
        }

        steps.Add(new ScaffoldStep(ExerciseType.MeaningToWordScramble));
        steps.Add(new ScaffoldStep(ExerciseType.WordToMeaningReveal));

        return steps;
    }
}
