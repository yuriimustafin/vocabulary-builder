namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// How much trouble a word is giving the learner. Derived from ease factor,
/// lapses since recovery and recent success rate; controls how many ungraded
/// follow-up exercises run after the graded probe.
/// </summary>
public enum CardDifficulty
{
    Comfortable = 0,
    Shaky = 1,
    Difficult = 2
}
