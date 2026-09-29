using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// The exercise a card is to be graded on now.
/// </summary>
/// <param name="Rung">The level the exercise was taken from.</param>
/// <param name="Type">The exercise.</param>
/// <param name="Escalated">
/// Raised to unhinted production after a long absence. Offered no hint, because a cue there
/// would inflate a grade that is about to stretch the interval a long way.
/// </param>
/// <param name="CueLevel">
/// How far the support has faded on this level: zero for a word just arrived, one more per
/// clean success. An exercise with a graded cue, such as partial letters, shows less of the
/// word the higher this is.
/// </param>
public record ProbeChoice(int Rung, ExerciseType Type, bool Escalated, int CueLevel);

/// <summary>Where a card stands on the ladder after a graded answer.</summary>
/// <param name="Rung">The level.</param>
/// <param name="Streak">Clean successes in a row on that level.</param>
public record RungMove(int Rung, int Streak);

public interface IExerciseLadder
{
    int RungCount { get; }

    /// <summary>The top level, where a word stays once it gets there.</summary>
    int TopRung { get; }

    /// <summary>The first exercise a level asks - the one follow-ups replay.</summary>
    ExerciseType TypeAt(int rung);

    /// <summary>Every exercise a level can ask, easiest first.</summary>
    IReadOnlyList<ExerciseType> TypesAt(int rung);

    /// <summary>
    /// Picks the exercise to grade the card on now. Honours the card's own level and how far
    /// it has got on it, never repeats the last exercise when the level has another,
    /// escalates after a long absence, and falls back down when nothing on the level can be
    /// built from the material available for the word.
    /// </summary>
    ProbeChoice SelectProbe(ReviewCard card, string? partOfSpeech, DateTime nowUtc, Func<ExerciseType, bool> canBuild);

    /// <summary>Where the card moves after a graded answer.</summary>
    RungMove NextRung(ReviewCard card, ReviewGrade grade, bool hintUsed);

    /// <summary>Whether a miss on this exercise costs the word nothing - see <see cref="LadderExerciseOptions.Tolerant"/>.</summary>
    bool IsTolerant(ExerciseType type);
}
