namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// Scheduling state of a <see cref="Domain.Entities.Study.ReviewCard"/>.
/// </summary>
public enum CardState
{
    /// <summary>Created but not yet answered once.</summary>
    New = 0,

    /// <summary>Working through the initial same-day learning steps.</summary>
    Learning = 1,

    /// <summary>Graduated - on a day-scale interval.</summary>
    Review = 2,

    /// <summary>Lapsed and working back through the learning steps.</summary>
    Relearning = 3,

    /// <summary>Excluded from the queue until manually resumed.</summary>
    Suspended = 4
}
