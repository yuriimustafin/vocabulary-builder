using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// The plain flashcard: see the word, recall what it means, reveal, judge.
///
/// This is the bottom of the ladder and the fallback for every rung above it, so it asks
/// for as little material as possible - a meaning and nothing else.
/// </summary>
public class WordToMeaningRevealExerciseDefinition : SelfGradedExerciseDefinition
{
    public override ExerciseType Type => ExerciseType.WordToMeaningReveal;

    public override bool CanBuild(StudyMaterial material, DistractorSet? distractors) => material.HasMeaning;

    public override ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context) => new()
    {
        Type = Type,
        GradingMode = GradingMode,
        WordId = material.WordId,
        Prompt = material.Headword,
        Answer = material.Meaning,
        Transcription = material.Transcription,
        PartOfSpeech = material.PartOfSpeech,
        ContextSentence = material.ContextSentence
    };
}
