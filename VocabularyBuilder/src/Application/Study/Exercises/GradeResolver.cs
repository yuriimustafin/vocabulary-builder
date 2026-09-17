using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// What an automatically graded exercise observed about the answer.
/// </summary>
/// <param name="Correct">Whether the final answer matched the target.</param>
/// <param name="ElapsedMs">Time from the exercise being shown to the answer being committed.</param>
/// <param name="Resets">How many times the learner cleared a part-built answer.</param>
/// <param name="Abandoned">The learner gave up rather than answering.</param>
public record AutoGradeSignals(bool Correct, int ElapsedMs, int Resets = 0, bool Abandoned = false);

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
        if (type == ExerciseType.MeaningToWordScramble && signals.Resets > 0)
        {
            return ReviewGrade.Hard;
        }

        if (signals.ElapsedMs <= _options.FastAnswerMs)
        {
            return ReviewGrade.Easy;
        }

        return signals.ElapsedMs >= _options.SlowAnswerMs ? ReviewGrade.Hard : ReviewGrade.Good;
    }
}
