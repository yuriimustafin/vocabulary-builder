namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// Generation state of the AI-filled gaps in a word's study material.
/// </summary>
public enum StudyContentStatus
{
    /// <summary>Queued or in flight.</summary>
    Pending = 0,

    /// <summary>Usable - every gap that was detected has been filled.</summary>
    Ready = 1,

    /// <summary>Generation failed too many times; the word is skipped rather than retried forever.</summary>
    Failed = 2
}
