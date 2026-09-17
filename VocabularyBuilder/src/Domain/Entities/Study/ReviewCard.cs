using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.Study;

/// <summary>
/// The spaced-repetition state of a single word.
/// Exactly one card per word (ladder style): the exercise type is chosen by the word's
/// current strength rather than scheduled separately per exercise.
/// A word with no card has simply not been introduced yet - the row is created at introduction.
/// </summary>
public class ReviewCard : BaseAuditableEntity
{
    public int WordId { get; set; }

    public Word Word { get; set; } = null!;

    public CardState State { get; set; } = CardState.New;

    /// <summary>
    /// Position in the configured exercise ladder. Climbs on success, falls back on failure,
    /// so a word that starts giving trouble sees its earlier, easier exercises again.
    /// </summary>
    public int CurrentRung { get; set; }

    /// <summary>SM-2 ease factor. Floored by StudyOptions.MinEaseFactor.</summary>
    public double EaseFactor { get; set; } = 2.5;

    /// <summary>Current scheduling interval in days. Zero while still in learning steps.</summary>
    public int IntervalDays { get; set; }

    /// <summary>Learning steps completed so far, while Learning or Relearning.</summary>
    public int LearningStepIndex { get; set; }

    /// <summary>Count of graded (non-follow-up) reviews. Statistics only - the ladder uses CurrentRung.</summary>
    public int ReviewNumber { get; set; }

    /// <summary>Lifetime count of failed reviews.</summary>
    public int Lapses { get; set; }

    /// <summary>
    /// Failures since the card last evaluated as Comfortable. Reset on recovery so that
    /// one bad week does not mark a word as difficult forever.
    /// </summary>
    public int LapsesSinceRecovery { get; set; }

    /// <summary>
    /// Exponential moving average of graded review success, in [0,1].
    /// Denormalised onto the card so the queue builder can compute a difficulty tier
    /// without aggregating review history for every queued card.
    /// </summary>
    public double RecentSuccessRate { get; set; } = 1.0;

    // SQLite cannot ORDER BY a DateTimeOffset (see GetWords.cs), and the due-card query
    // has to filter and sort in SQL, so these three are UTC DateTime.

    public DateTime? DueAtUtc { get; set; }

    public DateTime? LastReviewedAtUtc { get; set; }

    public DateTime IntroducedAtUtc { get; set; }
}
