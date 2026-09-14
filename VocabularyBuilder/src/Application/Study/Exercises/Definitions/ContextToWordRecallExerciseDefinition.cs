using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// Cloze: the word is cut out of a real sentence and has to be put back.
///
/// The meaning sits behind a hint the learner can choose to open, which makes the cue
/// strength their own decision. Whether they took it is recorded, and an escalated probe
/// withholds it entirely so the grade reflects memory rather than the prompt.
/// </summary>
public class ContextToWordRecallExerciseDefinition : SelfGradedExerciseDefinition
{
    public override ExerciseType Type => ExerciseType.ContextToWordRecall;

    /// <summary>
    /// The resolver only ever supplies a sentence that contains the headword, so there is
    /// always something to blank out.
    /// </summary>
    public override bool CanBuild(StudyMaterial material, DistractorSet? distractors) => material.HasContextSentence;

    public override ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context) => new()
    {
        Type = Type,
        GradingMode = GradingMode,
        WordId = material.WordId,
        Prompt = HeadwordText.Blankify(material.ContextSentence!, material.Headword),
        Answer = material.Headword,
        Hint = context.AllowHint ? material.Meaning : null,
        PartOfSpeech = material.PartOfSpeech,
        Transcription = material.Transcription,
        ContextSentence = material.ContextSentence
    };
}
