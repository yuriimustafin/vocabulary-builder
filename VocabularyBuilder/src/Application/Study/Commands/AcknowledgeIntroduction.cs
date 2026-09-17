using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Scheduling;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Commands;

/// <summary>
/// Records that a word has been met for the first time.
/// </summary>
public record AcknowledgeIntroductionCommand : IRequest<IntroductionResultDto>
{
    public int CardId { get; init; }

    /// <summary>Identifies this rendering, so a repeat acknowledgement counts once.</summary>
    public Guid AttemptId { get; init; }

    public ExerciseType ExerciseType { get; init; }

    public int ElapsedMs { get; init; }
}

public class IntroductionResultDto
{
    public CardState State { get; init; }
    public int Rung { get; init; }
    public DateTime? NextDueAtUtc { get; init; }
    public bool WasDuplicate { get; init; }
}

/// <summary>
/// The first time a word is shown there is nothing to assess: the learner is reading it,
/// not recalling it. Asking them to grade that has no honest answer - "Again" records a
/// failure on a word that was never known and cuts its ease, "Good" claims a recall that
/// never happened - and either way the card's history opens with a fiction that the
/// scheduler then treats as evidence.
///
/// So the first showing is an acknowledgement. The word is put on the first learning step
/// and moved up a rung, which means the next time it comes round, a minute later, it is
/// genuinely being tested. Ease, lapses and the success average are left alone until then.
/// </summary>
public class AcknowledgeIntroductionCommandHandler
    : IRequestHandler<AcknowledgeIntroductionCommand, IntroductionResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IReviewScheduler _scheduler;
    private readonly IExerciseLadder _ladder;
    private readonly TimeProvider _timeProvider;

    public AcknowledgeIntroductionCommandHandler(
        IApplicationDbContext context,
        IReviewScheduler scheduler,
        IExerciseLadder ladder,
        TimeProvider timeProvider)
    {
        _context = context;
        _scheduler = scheduler;
        _ladder = ladder;
        _timeProvider = timeProvider;
    }

    public async Task<IntroductionResultDto> Handle(
        AcknowledgeIntroductionCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var card = await _context.ReviewCards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken);

        Guard.Against.NotFound(request.CardId, card);

        if (await _context.ReviewLogs.AnyAsync(l => l.AttemptId == request.AttemptId, cancellationToken))
        {
            return Result(card, wasDuplicate: true);
        }

        var stateBefore = card.State;
        var rungBefore = card.CurrentRung;
        var scheduling = _scheduler.Introduce(card, now);

        card.State = scheduling.State;
        card.IntervalDays = scheduling.IntervalDays;
        card.LearningStepIndex = scheduling.LearningStepIndex;
        card.DueAtUtc = scheduling.DueAtUtc;
        card.LastReviewedAtUtc = now;

        // Climbs unconditionally: the 85% rule weighs a record this word does not have yet.
        card.CurrentRung = Math.Min(rungBefore + 1, _ladder.RungCount - 1);

        // Logged as support rather than assessment, so it stays out of every statistic that
        // judges how well the word is known.
        _context.ReviewLogs.Add(new ReviewLog
        {
            ReviewCardId = card.Id,
            AttemptId = request.AttemptId,
            ReviewedAtUtc = now,
            ExerciseType = request.ExerciseType,
            Grade = ReviewGrade.Good,
            GradeWasSelfReported = false,
            IsScaffold = true,
            ElapsedMs = request.ElapsedMs,
            StateBefore = stateBefore,
            RungBefore = rungBefore,
            IntervalBeforeDays = 0,
            IntervalAfterDays = card.IntervalDays,
            EaseFactorAfter = card.EaseFactor
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result(card, wasDuplicate: false);
    }

    private static IntroductionResultDto Result(ReviewCard card, bool wasDuplicate) => new()
    {
        State = card.State,
        Rung = card.CurrentRung,
        NextDueAtUtc = card.DueAtUtc,
        WasDuplicate = wasDuplicate
    };
}
