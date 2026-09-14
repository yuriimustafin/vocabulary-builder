using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// Shared base for exercises the learner grades themselves. There is nothing to mark:
/// the answer is revealed and the learner reports how it went, which is the only way to
/// grade a free recall that happened in their head.
/// </summary>
public abstract class SelfGradedExerciseDefinition : IExerciseDefinition
{
    public abstract ExerciseType Type { get; }

    public GradingMode GradingMode => GradingMode.SelfReported;

    public virtual bool CanBeProbe => true;

    public abstract bool CanBuild(StudyMaterial material, DistractorSet? distractors);

    public abstract ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context);

    /// <summary>
    /// A missing judgement is treated as a failure rather than silently credited, so an
    /// abandoned session can never inflate a card.
    /// </summary>
    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material) =>
        answer.SelfGrade ?? ReviewGrade.Again;
}
