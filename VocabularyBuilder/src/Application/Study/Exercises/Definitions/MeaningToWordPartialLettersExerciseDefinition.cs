using System.Globalization;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// The meaning plus part of the spelling, with the rest masked.
///
/// This exists only as a step in the diminishing-cues sequence that runs after a word is
/// failed: the same word is re-attempted with progressively more of it showing until it
/// can be produced. It is never selected as the graded attempt, because the point of it is
/// to hand out support rather than to measure anything.
/// </summary>
public class MeaningToWordPartialLettersExerciseDefinition : SelfGradedExerciseDefinition
{
    public override ExerciseType Type => ExerciseType.MeaningToWordPartialLetters;

    public override bool CanBeProbe => false;

    public override bool CanBuild(StudyMaterial material, DistractorSet? distractors) => material.HasMeaning;

    public override ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context) => new()
    {
        Type = Type,
        GradingMode = GradingMode,
        WordId = material.WordId,
        Prompt = material.Meaning!,
        Answer = material.Headword,
        LetterMask = Mask(material.Headword, context.RevealedLetters),
        PartOfSpeech = material.PartOfSpeech,
        ContextSentence = material.ContextSentence,
        ContextSentenceTranslation = material.ContextSentenceTranslation,
        MeaningGloss = material.MeaningGloss
    };

    /// <summary>
    /// Builds the partially revealed spelling, for example "d _ _ _ _ _ _".
    /// Text elements rather than chars, so an accented letter is revealed whole.
    /// </summary>
    internal static string Mask(string headword, int revealedLetters)
    {
        var parts = new List<string>();
        var revealed = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(headword.Trim());

        while (enumerator.MoveNext())
        {
            var element = (string)enumerator.Current;

            if (string.IsNullOrWhiteSpace(element))
            {
                // Word boundaries in a phrase are structure, not a clue worth hiding.
                parts.Add("/");
                continue;
            }

            if (revealed < revealedLetters)
            {
                parts.Add(element);
                revealed++;
            }
            else
            {
                parts.Add("_");
            }
        }

        return string.Join(' ', parts);
    }
}
