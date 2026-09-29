using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// What an automatically graded exercise observed about the answer.
/// </summary>
/// <param name="Correct">Whether the final answer matched the target.</param>
/// <param name="ElapsedMs">Time from the exercise being shown to the answer being committed.</param>
/// <param name="Resets">How many times the learner cleared a part-built answer.</param>
/// <param name="Abandoned">The learner gave up rather than answering.</param>
/// <param name="Length">Letters in the word, which sets how long building it may fairly take.</param>
public record AutoGradeSignals(bool Correct, int ElapsedMs, int Resets = 0, bool Abandoned = false, int Length = 0);

public interface IGradeResolver
{
    ReviewGrade Resolve(ExerciseType type, AutoGradeSignals signals);
}

/// <summary>
/// Maps the observable signals of an automatically graded exercise onto the four grades
/// the scheduler understands. Self-graded exercises bypass this entirely - the learner's
/// own judgement is used as given.
/// </summary>
public class GradeResolver : IGradeResolver
{
    private readonly StudyOptions _options;

    public GradeResolver(StudyOptions options) => _options = options;

    public ReviewGrade Resolve(ExerciseType type, AutoGradeSignals signals)
    {
        if (signals.Abandoned || !signals.Correct)
        {
            return ReviewGrade.Again;
        }

        // Assembling the word from tiles is judged on cleanliness first: needing to start
        // over means the spelling was not actually known, however quickly it ended up right.
        if (type is ExerciseType.MeaningToWordScramble
                or ExerciseType.MeaningToWordSyllableScramble
                or ExerciseType.TranslationToSentenceScramble
            && signals.Resets > 0)
        {
            return ReviewGrade.Hard;
        }

        // Picking from options can never be Easy. A quick click may come from ruling out the
        // other options or from a vague sense of familiarity, and says nothing about whether
        // the word could be produced. Easy is what lets a card skip ahead, so it is reserved
        // for exercises where the learner builds the word.
        if (signals.ElapsedMs <= _options.FastAnswerMs)
        {
            return IsRecognition(type) ? ReviewGrade.Good : ReviewGrade.Easy;
        }

        return signals.ElapsedMs >= SlowThreshold(type, signals.Length) ? ReviewGrade.Hard : ReviewGrade.Good;
    }

    /// <summary>
    /// Building a word goes piece by piece, so a long word takes longer however well it is
    /// known. Holding it to the same limit as a click would mark long words down for their
    /// length and keep them from ever counting as clean.
    /// </summary>
    private int SlowThreshold(ExerciseType type, int length) =>
        IsBuilt(type)
            ? Math.Max(_options.SlowAnswerMs, length * _options.SlowAnswerMsPerLetter)
            : _options.SlowAnswerMs;

    private static bool IsRecognition(ExerciseType type) =>
        type is ExerciseType.WordToMeaningChoice
            or ExerciseType.MeaningToWordChoice
            or ExerciseType.ContextToWordChoice
            or ExerciseType.WordToCollocatesChoice;

    private static bool IsBuilt(ExerciseType type) =>
        type is ExerciseType.MeaningToWordScramble
            or ExerciseType.MeaningToWordSyllableScramble
            or ExerciseType.MeaningToWordType
            or ExerciseType.MeaningToWordCuedType
            or ExerciseType.TranslationToSentenceScramble;
}
