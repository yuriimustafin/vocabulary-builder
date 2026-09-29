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
    /// Level on the configured exercise ladder - the introduction, then recognition,
    /// scaffolded production and production. Each level holds a pool of exercises. A word
    /// stays on its level until it has enough clean successes there, and drops back one
    /// level on a miss, so it meets the easier exercises again.
    /// </summary>
    public int CurrentRung { get; set; }

    /// <summary>
    /// Clean successes in a row on the current level: correct, no hint, not slow. Moves the
    /// word up once the level's quota is met, and picks progressively harder exercises from
    /// the level's pool on the way there. Reset by a miss and by every change of level.
    /// </summary>
    public int RungStreak { get; set; }

    /// <summary>The exercise the word was last graded on, so the next one can be different.</summary>
    public ExerciseType? LastExerciseType { get; set; }

    /// <summary>
    /// Graded tries since the word last entered learning or relearning. Caps how long one
    /// word can hold a session; reset when it graduates.
    /// </summary>
    public int PhaseRetrievals { get; set; }

    /// <summary>SM-2 ease factor. Floored by StudyOptions.MinEaseFactor.</summary>
    public double EaseFactor { get; set; } = 2.5;

    /// <summary>Current scheduling interval in days. Zero while still in learning steps.</summary>
    public int IntervalDays { get; set; }

    /// <summary>
    /// Position in the learning steps while Learning or Relearning, which sets how long until
    /// the next try. Back to the start on a miss; the last step repeats until the word
    /// meets its exit criterion.
    /// </summary>
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
