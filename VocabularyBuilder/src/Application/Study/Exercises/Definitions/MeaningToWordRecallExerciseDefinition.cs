using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// Free production: the meaning is shown, the word has to come from memory, and only then
/// is it revealed.
///
/// This is the top of the ladder and the probe used after a long absence. It offers no cue
/// at all, which is what makes it both the most demanding retrieval and the only reading
/// of memory that is not inflated by the prompt.
/// </summary>
public class MeaningToWordRecallExerciseDefinition : SelfGradedExerciseDefinition
{
    public override ExerciseType Type => ExerciseType.MeaningToWordRecall;

    public override bool CanBuild(StudyMaterial material, DistractorSet? distractors) => material.HasMeaning;

    public override ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context) => new()
    {
        Type = Type,
        GradingMode = GradingMode,
        WordId = material.WordId,
        Prompt = material.Meaning!,
        Answer = material.Headword,
        // Deliberately no hint: a cue here would defeat the point of the exercise.
        Hint = null,
        PartOfSpeech = material.PartOfSpeech,
        Transcription = material.Transcription,
        ContextSentence = material.ContextSentence,
        ContextSentenceTranslation = material.ContextSentenceTranslation,
        MeaningGloss = material.MeaningGloss
    };
}
