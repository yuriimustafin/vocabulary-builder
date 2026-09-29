using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// One ungraded re-encoding exercise served after the graded probe.
/// </summary>
/// <param name="Type">Exercise to render.</param>
/// <param name="RevealedLetters">
/// Letters to show for <see cref="ExerciseType.MeaningToWordPartialLetters"/>; ignored otherwise.
/// </param>
/// <param name="Alternatives">
/// Other exercises that would serve, in order, for when <paramref name="Type"/> cannot be
/// built for this word - a level replayed as a whole rather than one exercise from it.
/// </param>
public record ScaffoldStep(ExerciseType Type, int RevealedLetters = 0, IReadOnlyList<ExerciseType>? Alternatives = null)
{
    /// <summary>The exercise first, then its alternatives.</summary>
    public IEnumerable<ExerciseType> Candidates =>
        new[] { Type }.Concat(Alternatives ?? Array.Empty<ExerciseType>()).Distinct();
}

public interface IScaffoldSequencer
{
    /// <param name="tolerated">
    /// The miss was on a mistake-tolerant exercise and cost the word nothing. Only the
    /// word's connections follow: the word comes back shortly to be asked another way, so
    /// there is no need to walk it through its letters now.
    /// </param>
    IReadOnlyList<ScaffoldStep> Build(
        int probeRung, ReviewGrade grade, CardDifficulty difficulty, int headwordLength, bool tolerated = false);
}

/// <summary>
/// Builds the follow-up sequence that runs after a graded probe.
///
/// A failure triggers diminishing-cues retrieval practice: the word is re-attempted with
/// progressively more of it revealed until it can be produced. That pattern is documented
/// to help in exactly the situation a plain retry does not - a word that was just missed.
///
/// A merely shaky word gets a gentler version: the levels just below the probe, replayed
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

    public IReadOnlyList<ScaffoldStep> Build(
        int probeRung, ReviewGrade grade, CardDifficulty difficulty, int headwordLength, bool tolerated = false)
    {
        if (grade == ReviewGrade.Again)
        {
            return tolerated
                ? new List<ScaffoldStep> { new(ExerciseType.WordToConnectionsReveal) }
                : DiminishingCues(headwordLength);
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
            .Select(rung => new ScaffoldStep(_ladder.TypeAt(rung), Alternatives: _ladder.TypesAt(rung)))
            .ToList();
    }

    /// <summary>
    /// First what ties the word to things already known - its mnemonic, where it comes
    /// from - and then it is asked again with cues that shrink step by step: a first letter,
    /// then roughly half the word, then the letters shuffled as tiles, and finally the whole
    /// word alongside its meaning. A word with no connections starts at the cues.
    /// </summary>
    private static List<ScaffoldStep> DiminishingCues(int headwordLength)
    {
        var steps = new List<ScaffoldStep> { new(ExerciseType.WordToConnectionsReveal) };

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
