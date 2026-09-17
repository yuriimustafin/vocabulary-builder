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

        var cardIds = cards.Select(c => c.Id).ToList();

        var logs = await _context.ReviewLogs
            .Where(l => cardIds.Contains(l.ReviewCardId))
            .ToListAsync(cancellationToken);

        if (request.Scope == ClearStudyScope.All)
        {
            _context.ReviewLogs.RemoveRange(logs);
            _context.ReviewCards.RemoveRange(cards);
            await _context.SaveChangesAsync(cancellationToken);

            return new ClearStudyProgressResultDto
            {
                CardsRemoved = cards.Count,
                ReviewsRemoved = logs.Count
            };
        }

        var dayStart = StudyDay.StartOf(_timeProvider.GetUtcNow().UtcDateTime, _options.DayRolloverHourUtc);

        // A word first met today goes back to never having been studied.
        var introducedToday = cards.Where(c => c.IntroducedAtUtc >= dayStart).ToList();
        var introducedTodayIds = introducedToday.Select(c => c.Id).ToHashSet();

        var todaysLogs = logs.Where(l => l.ReviewedAtUtc >= dayStart).ToList();

        // An older word answered today keeps the state those answers left it in. Rewinding
        // it would take an ease and a success average that are not recorded per review, and
        // a plausible guess at them would be worse than saying so.
        var olderCardsTouchedToday = todaysLogs
            .Select(l => l.ReviewCardId)
            .Where(id => !introducedTodayIds.Contains(id))
            .Distinct()
            .Count();

        _context.ReviewLogs.RemoveRange(todaysLogs);
        _context.ReviewCards.RemoveRange(introducedToday);
        await _context.SaveChangesAsync(cancellationToken);

        return new ClearStudyProgressResultDto
        {
            CardsRemoved = introducedToday.Count,
            ReviewsRemoved = todaysLogs.Count,
            CardsLeftAdvanced = olderCardsTouchedToday
        };
    }
}
