using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Commands;

public enum ClearStudyScope
{
    /// <summary>Words introduced today, so the day can be started over.</summary>
    Today = 0,

    /// <summary>Every card and every review, back to nothing having been studied.</summary>
    All = 1
}

/// <summary>
/// Throws away study progress so a session can be tried again from a known state.
///
/// Only the scheduling is removed. Words themselves are untouched, and so is any content
/// that had to be generated for them - that cost a model call to produce, and clearing it
/// would mean paying for it again.
/// </summary>
public record ClearStudyProgressCommand(Language Language, ClearStudyScope Scope)
    : IRequest<ClearStudyProgressResultDto>;

public class ClearStudyProgressResultDto
{
    public int CardsRemoved { get; init; }
    public int ReviewsRemoved { get; init; }

    /// <summary>
    /// Cards introduced before today that were reviewed today. Their reviews are gone but
    /// the cards keep whatever state those reviews left them in, so this is worth saying.
    /// </summary>
    public int CardsLeftAdvanced { get; init; }
}

public class ClearStudyProgressCommandHandler
    : IRequestHandler<ClearStudyProgressCommand, ClearStudyProgressResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly StudyOptions _options;
    private readonly TimeProvider _timeProvider;

    public ClearStudyProgressCommandHandler(
        IApplicationDbContext context, StudyOptions options, TimeProvider timeProvider)
    {
        _context = context;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<ClearStudyProgressResultDto> Handle(
        ClearStudyProgressCommand request, CancellationToken cancellationToken)
    {
        var cards = await _context.ReviewCards
            .Where(c => c.Word.Language == request.Language)
            .ToListAsync(cancellationToken);

        // Every answer still in play for the language, including those of words whose card is
        // already gone. They are voided rather than deleted: the progress is what is being
        // thrown away, not the record that the answers were given
        var logs = await _context.ReviewLogs
            .Where(l => l.Word.Language == request.Language && l.VoidedAtUtc == null)
            .ToListAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (request.Scope == ClearStudyScope.All)
        {
            Void(logs, now, "ClearedAll");
            _context.ReviewCards.RemoveRange(cards);
            _context.RecordActivity(
                ActivityAction.StudyProgressCleared,
                language: request.Language,
                summary: $"All progress cleared: {cards.Count} cards, {logs.Count} reviews",
                details: new { request.Scope, cardsRemoved = cards.Count, reviewsRemoved = logs.Count });
            await _context.SaveChangesAsync(cancellationToken);

            return new ClearStudyProgressResultDto
            {
                CardsRemoved = cards.Count,
                ReviewsRemoved = logs.Count
            };
        }

        var dayStart = StudyDay.StartOf(now, _options.DayRolloverHourUtc);

        // A word first met today goes back to never having been studied.
        var introducedToday = cards.Where(c => c.IntroducedAtUtc >= dayStart).ToList();
        var introducedTodayIds = introducedToday.Select(c => c.Id).ToHashSet();

        var todaysLogs = logs.Where(l => l.ReviewedAtUtc >= dayStart).ToList();

        // An older word answered today keeps the state those answers left it in. Rewinding
        // it would take an ease and a success average that are not recorded per review, and
        // a plausible guess at them would be worse than saying so.
        var olderCardsTouchedToday = todaysLogs
            .Where(l => l.ReviewCardId != null)
            .Select(l => l.ReviewCardId!.Value)
            .Where(id => !introducedTodayIds.Contains(id))
            .Distinct()
            .Count();

        Void(todaysLogs, now, "ClearedToday");
        _context.ReviewCards.RemoveRange(introducedToday);
        _context.RecordActivity(
            ActivityAction.StudyProgressCleared,
            language: request.Language,
            summary: $"Today cleared: {introducedToday.Count} cards, {todaysLogs.Count} reviews",
            details: new { request.Scope, cardsRemoved = introducedToday.Count, reviewsRemoved = todaysLogs.Count });
        await _context.SaveChangesAsync(cancellationToken);

        return new ClearStudyProgressResultDto
        {
            CardsRemoved = introducedToday.Count,
            ReviewsRemoved = todaysLogs.Count,
            CardsLeftAdvanced = olderCardsTouchedToday
        };
    }

    private static void Void(IEnumerable<Domain.Entities.Study.ReviewLog> logs, DateTime now, string reason)
    {
        foreach (var log in logs)
        {
            log.VoidedAtUtc = now;
            log.VoidReason = reason;
        }
    }
}
