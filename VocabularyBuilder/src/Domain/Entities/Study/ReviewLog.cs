using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.Study;

/// <summary>
/// Append-only record of a single answer, written for graded probes and ungraded
/// follow-ups alike. This is the only part of the feature that cannot be reconstructed
/// later, and it carries everything a different scheduling algorithm would need to be
/// fitted against real history.
///
/// It belongs to the word, not to the card. A card is scheduling state and comes and goes - a
/// word marked as known loses it, clearing study progress removes it - and the answers given
/// before that are still answers that were given. So the card link is cleared rather than
/// cascaded, and a log taken out of play is voided rather than deleted.
/// </summary>
public class ReviewLog : BaseEntity
{
    public int WordId { get; set; }

    public Word Word { get; set; } = null!;

    /// <summary>The card the answer was given on, null once that card is gone.</summary>
    public int? ReviewCardId { get; set; }

    public ReviewCard? ReviewCard { get; set; }

    /// <summary>
    /// Client-generated per rendered exercise. Unique, so a double submit
    /// (double click, retry after a timeout) scores the card once.
    /// </summary>
    public Guid AttemptId { get; set; }

    public DateTime ReviewedAtUtc { get; set; }

    public ExerciseType ExerciseType { get; set; }

    public ReviewGrade Grade { get; set; }

    public bool GradeWasSelfReported { get; set; }

    /// <summary>
    /// True for re-encoding follow-ups that run after the graded probe.
    /// These never affect ease, interval, rung or success rate.
    /// </summary>
    public bool IsScaffold { get; set; }

    public int ElapsedMs { get; set; }

    public bool HintUsed { get; set; }

    public CardState StateBefore { get; set; }

    public int RungBefore { get; set; }

    public int IntervalBeforeDays { get; set; }

    public int IntervalAfterDays { get; set; }

    public double EaseFactorAfter { get; set; }

    /// <summary>The level the word was left on.</summary>
    public int RungAfter { get; set; }

    /// <summary>
    /// What was answered, as given: the typed word, the option picked, or the tiles in the
    /// order they were put - null where the exercise has no answer of that kind.
    /// </summary>
    public string? Answer { get; set; }

    /// <summary>How a typed answer was marked: Exact, AccentsOnly, Typo, WrongArticle, Wrong.</summary>
    public string? AnswerMatch { get; set; }

    /// <summary>The stored example sentence the exercise was asked on, if any. Kept when it is not.</summary>
    public int? StudyExampleId { get; set; }

    /// <summary>A miss on a mistake-tolerant exercise, which cost the word nothing.</summary>
    public bool Tolerated { get; set; }

    /// <summary>
    /// Set when the answer was taken out of play - the day's progress cleared - so it no longer
    /// counts towards anything, while still saying it was given.
    /// </summary>
    public DateTime? VoidedAtUtc { get; set; }

    /// <summary>Why it was voided: ClearedToday or ClearedAll.</summary>
    public string? VoidReason { get; set; }
}
