using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

public interface IExerciseLadder
{
    int RungCount { get; }

    ExerciseType TypeAt(int rung);

    /// <summary>
    /// Picks the exercise to grade the card on now. Honours the card's own position,
    /// escalates after a long absence, and falls back down when the chosen rung cannot
    /// be built from the material available for the word.
    /// </summary>
    int SelectProbeRung(ReviewCard card, string? partOfSpeech, DateTime nowUtc, Func<ExerciseType, bool> canBuild);

    /// <summary>Where the card's rung moves after a graded review.</summary>
    int NextRung(int currentRung, ReviewGrade grade, double recentSuccessRate);
}
