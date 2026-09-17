namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// Where an exercise's grade comes from.
/// </summary>
public enum GradingMode
{
    /// <summary>The learner picks Again/Hard/Good/Easy after revealing the answer.</summary>
    SelfReported = 0,

    /// <summary>The grade is derived from the answer itself (correctness, speed, hints used).</summary>
    Automatic = 1
}
