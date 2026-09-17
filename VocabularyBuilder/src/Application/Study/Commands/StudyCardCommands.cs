using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Commands;

/// <summary>
/// Records one ungraded re-encoding attempt. Kept out of the card's statistics entirely -
/// it exists so the history shows what support was given, not to judge the learner.
/// </summary>
public record RecordFollowUpCommand : IRequest<bool>
{
    public int CardId { get; init; }
    public Guid AttemptId { get; init; }
    public ExerciseType ExerciseType { get; init; }
    public int ElapsedMs { get; init; }
    public bool Correct { get; init; }
}

public class RecordFollowUpCommandHandler : IRequestHandler<RecordFollowUpCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public RecordFollowUpCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<bool> Handle(RecordFollowUpCommand request, CancellationToken cancellationToken)
    {
        var card = await _context.ReviewCards.FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken);

        if (card is null)
        {
            return false;
        }

        if (await _context.ReviewLogs.AnyAsync(l => l.AttemptId == request.AttemptId, cancellationToken))
        {
            return true;
        }

        _context.ReviewLogs.Add(new Domain.Entities.Study.ReviewLog
        {
            ReviewCardId = card.Id,
            AttemptId = request.AttemptId,
            ReviewedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            ExerciseType = request.ExerciseType,
            Grade = request.Correct ? ReviewGrade.Good : ReviewGrade.Again,
            IsScaffold = true,
            ElapsedMs = request.ElapsedMs,
            StateBefore = card.State,
            RungBefore = card.CurrentRung,
            IntervalBeforeDays = card.IntervalDays,
            IntervalAfterDays = card.IntervalDays,
            EaseFactorAfter = card.EaseFactor
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>Takes a word out of the rotation without losing its history.</summary>
public record SuspendCardCommand(int CardId, bool Suspended) : IRequest<bool>;

public class SuspendCardCommandHandler : IRequestHandler<SuspendCardCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SuspendCardCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<bool> Handle(SuspendCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _context.ReviewCards.FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken);

        if (card is null)
        {
            return false;
        }

        if (request.Suspended)
        {
            card.State = CardState.Suspended;
        }
        else
        {
            // Resuming puts the word back in front of the learner now rather than at
            // whatever moment it would have come up had it never been paused.
            card.State = card.IntervalDays > 0 ? CardState.Review : CardState.Learning;
            card.DueAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>
/// Starts a word over. The review history is deliberately kept: it is the record of what
/// happened, and a fresh start is a decision about the future rather than a correction of
/// the past.
/// </summary>
public record ResetCardCommand(int CardId) : IRequest<bool>;

public class ResetCardCommandHandler : IRequestHandler<ResetCardCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ResetCardCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<bool> Handle(ResetCardCommand request, CancellationToken cancellationToken)
    {
        var card = await _context.ReviewCards.FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken);

        if (card is null)
        {
            return false;
        }

        card.State = CardState.New;
        card.CurrentRung = 0;
        card.EaseFactor = 2.5;
        card.IntervalDays = 0;
        card.LearningStepIndex = 0;
        card.RecentSuccessRate = 1.0;
        card.LapsesSinceRecovery = 0;
        card.DueAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>Queues a word for the next session, independent of the export pipeline.</summary>
public record MarkWordForStudyCommand(int WordId, bool Marked) : IRequest<bool>;

public class MarkWordForStudyCommandHandler : IRequestHandler<MarkWordForStudyCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public MarkWordForStudyCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(MarkWordForStudyCommand request, CancellationToken cancellationToken)
    {
        var word = await _context.Words.FirstOrDefaultAsync(w => w.Id == request.WordId, cancellationToken);

        if (word is null)
        {
            return false;
        }

        word.IsMarkedForStudy = request.Marked;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
