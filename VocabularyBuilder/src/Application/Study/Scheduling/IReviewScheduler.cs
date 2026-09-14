using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Scheduling;

/// <summary>
/// Decides when a card comes back. Implementations are pure functions of the card,
/// the grade and the current time, which keeps them testable and swappable - a different
/// algorithm fitted against ReviewLog history can replace SM-2 behind this interface.
/// </summary>
public interface IReviewScheduler
{
    SchedulingResult Schedule(ReviewCard card, ReviewGrade grade, DateTime nowUtc);
}
