namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// How well the learner recalled the word. Either self-reported or derived
/// from an automatically graded exercise.
/// </summary>
public enum ReviewGrade
{
    Again = 1,
    Hard = 2,
    Good = 3,
    Easy = 4
}
