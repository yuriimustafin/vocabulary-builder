using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Scheduling;

/// <summary>
/// SM-2 with the additions that make the first day bearable: same-day learning steps,
/// a separate relearning path after a lapse, an ease floor, interval fuzz so cards
/// introduced together do not stay clumped, and a maximum interval.
/// </summary>
public class Sm2Scheduler : IReviewScheduler
{
    private readonly StudyOptions _options;
    private readonly Random _random;

    public Sm2Scheduler(StudyOptions options) : this(options, Random.Shared)
    {
    }

    /// <summary>Test seam: supply a seeded Random to make fuzz deterministic.</summary>
    public Sm2Scheduler(StudyOptions options, Random random)
    {
        _options = options;
        _random = random;
    }

    public SchedulingResult Schedule(ReviewCard card, ReviewGrade grade, DateTime nowUtc)
    {
        return card.State switch
        {
            CardState.Review => ScheduleReview(card, grade, nowUtc),
            CardState.Relearning => ScheduleSteps(card, grade, nowUtc, CardState.Relearning),
            _ => ScheduleSteps(card, grade, nowUtc, CardState.Learning)
        };
    }

    /// <summary>
    /// New, Learning and Relearning cards walk the same minute-scale steps. The only
    /// difference is what interval they graduate to: a fresh card gets the graduating
    /// interval, a relearning card returns to whatever survived its lapse.
    /// </summary>
    /// <summary>
    /// Moves the word onto the first learning step, leaving ease untouched. A first meeting
    /// says nothing about how well it is known, so it must not look like evidence.
    /// </summary>
    public SchedulingResult Introduce(ReviewCard card, DateTime nowUtc) =>
        ScheduleSteps(card, ReviewGrade.Good, nowUtc, CardState.Learning);

    private SchedulingResult ScheduleSteps(ReviewCard card, ReviewGrade grade, DateTime nowUtc, CardState state)
    {
        var steps = Steps();
        var ease = card.EaseFactor;

        if (grade == ReviewGrade.Easy)
        {
            // Skip the remaining steps entirely.
            var easyInterval = state == CardState.Relearning
                ? Math.Max(card.IntervalDays, _options.EasyIntervalDays)
                : _options.EasyIntervalDays;
            return Graduate(easyInterval, ease, nowUtc);
        }

        // LearningStepIndex counts steps *completed*, so a brand new card sits at zero and
        // answering Good schedules it at the first step rather than skipping past it.
        var completed = grade switch
        {
            ReviewGrade.Again => 0,
            ReviewGrade.Hard => card.LearningStepIndex,
            _ => card.LearningStepIndex + 1
        };

        if (completed > steps.Length)
        {
            var graduatingInterval = state == CardState.Relearning
                ? Math.Max(1, card.IntervalDays)
                : _options.GraduatingIntervalDays;
            return Graduate(graduatingInterval, ease, nowUtc);
        }

        return new SchedulingResult(
            State: state,
            IntervalDays: card.IntervalDays,
            EaseFactor: ease,
            LearningStepIndex: completed,
            DueAtUtc: nowUtc.AddMinutes(steps[completed == 0 ? 0 : completed - 1]),
            IsLapse: false);
    }

    private SchedulingResult ScheduleReview(ReviewCard card, ReviewGrade grade, DateTime nowUtc)
    {
        if (grade == ReviewGrade.Again)
        {
            // Lapse: drop the ease, keep only the configured share of the interval, and
            // send the card back through the learning steps.
            var ease = ClampEase(card.EaseFactor - 0.20);
            var retained = Math.Max(1, (int)Math.Round(card.IntervalDays * (_options.LapseIntervalPercent / 100.0)));
            var steps = Steps();

            return new SchedulingResult(
                State: CardState.Relearning,
                IntervalDays: Math.Min(retained, _options.MaxIntervalDays),
                EaseFactor: ease,
                LearningStepIndex: 0,
                DueAtUtc: nowUtc.AddMinutes(steps.Length > 0 ? steps[0] : 0),
                IsLapse: true);
        }

        var newEase = ClampEase(card.EaseFactor + grade switch
        {
            ReviewGrade.Hard => -0.15,
            ReviewGrade.Easy => 0.15,
            _ => 0.0
        });

        var multiplier = grade switch
        {
            ReviewGrade.Hard => _options.HardIntervalMultiplier,
            ReviewGrade.Easy => newEase * _options.EasyBonus,
            _ => newEase
        };

        var previous = Math.Max(1, card.IntervalDays);
        // Always move forward: a repeat of the same interval would stall the card.
        var grown = Math.Max(previous + 1, (int)Math.Round(previous * multiplier));
        var interval = Math.Min(Fuzz(grown), _options.MaxIntervalDays);

        return new SchedulingResult(
            State: CardState.Review,
            IntervalDays: interval,
            EaseFactor: newEase,
            LearningStepIndex: 0,
            DueAtUtc: nowUtc.AddDays(interval),
            IsLapse: false);
    }

    private SchedulingResult Graduate(int intervalDays, double ease, DateTime nowUtc)
    {
        var interval = Math.Min(Math.Max(1, intervalDays), _options.MaxIntervalDays);
        return new SchedulingResult(
            State: CardState.Review,
            IntervalDays: interval,
            EaseFactor: ease,
            LearningStepIndex: 0,
            DueAtUtc: nowUtc.AddDays(interval),
            IsLapse: false);
    }

    private int[] Steps() => _options.EffectiveLearningSteps;

    private double ClampEase(double ease) => Math.Max(_options.MinEaseFactor, ease);

    /// <summary>
    /// Spreads intervals by up to IntervalFuzzPercent. Left alone below two days,
    /// where fuzz would only ever round back to the same value.
    /// </summary>
    private int Fuzz(int intervalDays)
    {
        if (_options.IntervalFuzzPercent <= 0 || intervalDays < 2)
        {
            return intervalDays;
        }

        var spread = intervalDays * (_options.IntervalFuzzPercent / 100.0);
        var offset = (_random.NextDouble() * 2 - 1) * spread;
        return Math.Max(1, (int)Math.Round(intervalDays + offset));
    }
}
