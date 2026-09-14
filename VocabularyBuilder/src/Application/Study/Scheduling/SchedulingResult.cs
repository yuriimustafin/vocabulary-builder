using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Scheduling;

/// <summary>
/// The new scheduling state for a card after one graded review.
/// A value, not a mutation: the caller decides when to write it back.
/// </summary>
public record SchedulingResult(
    CardState State,
    int IntervalDays,
    double EaseFactor,
    int LearningStepIndex,
    DateTime DueAtUtc,
    bool IsLapse);
