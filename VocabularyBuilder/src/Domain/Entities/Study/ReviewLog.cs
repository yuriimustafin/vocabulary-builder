using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.Study;

/// <summary>
/// Append-only record of a single answer, written for graded probes and ungraded
/// follow-ups alike. This is the only part of the feature that cannot be reconstructed
/// later, and it carries everything a different scheduling algorithm would need to be
/// fitted against real history.
/// </summary>
public class ReviewLog : BaseEntity
{
    public int ReviewCardId { get; set; }

    public ReviewCard ReviewCard { get; set; } = null!;

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
}
