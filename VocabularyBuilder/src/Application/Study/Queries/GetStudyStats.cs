using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Queries;

public record GetStudyStatsQuery(Language Language) : IRequest<StudyStatsDto>;

public class StudyStatsDto
{
    public int DueNow { get; init; }
    public int NewToday { get; init; }
    public int NewCardsPerDay { get; init; }

    /// <summary>Graded answers today. Follow-ups are excluded - they are support, not practice.</summary>
    public int ReviewedToday { get; init; }

    public int Learning { get; init; }

    /// <summary>Graduated but not yet on a long interval.</summary>
    public int Young { get; init; }

    /// <summary>Held for long enough to count as known.</summary>
    public int Mature { get; init; }

    public int Suspended { get; init; }

    /// <summary>Words never introduced, so never yet studied.</summary>
    public int NotStarted { get; init; }

    /// <summary>Words waiting to be filled in before they can be studied.</summary>
    public int AwaitingContent { get; init; }
}

public class GetStudyStatsQueryHandler : IRequestHandler<GetStudyStatsQuery, StudyStatsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly StudyOptions _options;
    private readonly TimeProvider _timeProvider;

    public GetStudyStatsQueryHandler(
        IApplicationDbContext context, StudyOptions options, TimeProvider timeProvider)
    {
        _context = context;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<StudyStatsDto> Handle(GetStudyStatsQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var dayStart = StudyDay.StartOf(now, _options.DayRolloverHourUtc);
        var mature = _options.MatureIntervalDays;

        var cards = _context.ReviewCards.Where(c => c.Word.Language == request.Language);

        return new StudyStatsDto
        {
            DueNow = await cards.CountAsync(
                c => c.State != CardState.Suspended && c.DueAtUtc != null && c.DueAtUtc <= now, cancellationToken),

            NewToday = await cards.CountAsync(c => c.IntroducedAtUtc >= dayStart, cancellationToken),
            NewCardsPerDay = _options.NewCardsPerDay,

            ReviewedToday = await _context.ReviewLogs.CountAsync(
                l => !l.IsScaffold && l.ReviewedAtUtc >= dayStart, cancellationToken),

            Learning = await cards.CountAsync(
                c => c.State == CardState.New || c.State == CardState.Learning || c.State == CardState.Relearning,
                cancellationToken),

            Young = await cards.CountAsync(
                c => c.State == CardState.Review && c.IntervalDays < mature, cancellationToken),

            Mature = await cards.CountAsync(
                c => c.State == CardState.Review && c.IntervalDays >= mature, cancellationToken),

            Suspended = await cards.CountAsync(c => c.State == CardState.Suspended, cancellationToken),

            NotStarted = await _context.Words.CountAsync(
                w => w.Language == request.Language && !_context.ReviewCards.Any(c => c.WordId == w.Id),
                cancellationToken),

            AwaitingContent = await _context.WordStudyContents.CountAsync(
                c => c.Status == StudyContentStatus.Pending && c.Word.Language == request.Language,
                cancellationToken)
        };
    }
}
