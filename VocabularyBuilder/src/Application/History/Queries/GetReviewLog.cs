using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.History.Queries;

/// <summary>One answer from the study history, as the History page shows it.</summary>
public class ReviewLogEntryDto
{
    public int Id { get; set; }
    public DateTime ReviewedAtUtc { get; set; }
    public int WordId { get; set; }
    public string Headword { get; set; } = string.Empty;
    public string ExerciseType { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;

    /// <summary>An unscored follow-up rather than a graded answer.</summary>
    public bool IsScaffold { get; set; }

    public bool HintUsed { get; set; }
    public bool Tolerated { get; set; }
    public string? Answer { get; set; }
    public string? AnswerMatch { get; set; }
    public int? StudyExampleId { get; set; }

    /// <summary>The example sentence it was asked on, if it is still stored.</summary>
    public string? ExampleSentence { get; set; }

    public int ElapsedMs { get; set; }
    public string StateBefore { get; set; } = string.Empty;
    public int RungBefore { get; set; }
    public int RungAfter { get; set; }
    public int IntervalBeforeDays { get; set; }
    public int IntervalAfterDays { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
}

/// <summary>
/// The study history, newest first: every answer and follow-up, read straight from the review
/// log rather than copied into the activity log, which a session would flood.
/// </summary>
public record GetReviewLogQuery : IRequest<PaginatedList<ReviewLogEntryDto>>
{
    public Language Language { get; init; }
    public int? WordId { get; init; }

    /// <summary>Unscored follow-ups too; off by default, since they outnumber the answers.</summary>
    public bool IncludeFollowUps { get; init; }

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetReviewLogQueryHandler : IRequestHandler<GetReviewLogQuery, PaginatedList<ReviewLogEntryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetReviewLogQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ReviewLogEntryDto>> Handle(GetReviewLogQuery request, CancellationToken cancellationToken)
    {
        var logs = _context.ReviewLogs
            .AsNoTracking()
            .Where(l => l.Word.Language == request.Language);

        if (request.WordId.HasValue)
        {
            logs = logs.Where(l => l.WordId == request.WordId);
        }

        if (!request.IncludeFollowUps)
        {
            logs = logs.Where(l => !l.IsScaffold);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var count = await logs.CountAsync(cancellationToken);

        // By id: the log is append-only, so id order is answer order
        var page = await logs
            .OrderByDescending(l => l.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new { Log = l, l.Word.Headword })
            .ToListAsync(cancellationToken);

        var items = await WithSentences(page.Select(p => (p.Log, p.Headword)).ToList(), cancellationToken);

        return new PaginatedList<ReviewLogEntryDto>(items, count, pageNumber, pageSize);
    }

    /// <summary>
    /// Maps the logs, with the sentence each was asked on. Looked up separately: an example can
    /// be replaced by a later generation, and the log keeps its id regardless.
    /// </summary>
    internal async Task<List<ReviewLogEntryDto>> WithSentences(
        IReadOnlyList<(ReviewLog Log, string Headword)> logs, CancellationToken cancellationToken)
    {
        var exampleIds = logs
            .Where(l => l.Log.StudyExampleId != null)
            .Select(l => l.Log.StudyExampleId!.Value)
            .Distinct()
            .ToList();

        var sentences = exampleIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.StudyExamples
                .AsNoTracking()
                .Where(e => exampleIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Sentence, cancellationToken);

        return logs.Select(l => new ReviewLogEntryDto
        {
            Id = l.Log.Id,
            ReviewedAtUtc = l.Log.ReviewedAtUtc,
            WordId = l.Log.WordId,
            Headword = l.Headword,
            ExerciseType = l.Log.ExerciseType.ToString(),
            Grade = l.Log.Grade.ToString(),
            IsScaffold = l.Log.IsScaffold,
            HintUsed = l.Log.HintUsed,
            Tolerated = l.Log.Tolerated,
            Answer = l.Log.Answer,
            AnswerMatch = l.Log.AnswerMatch,
            StudyExampleId = l.Log.StudyExampleId,
            ExampleSentence = l.Log.StudyExampleId is { } id ? sentences.GetValueOrDefault(id) : null,
            ElapsedMs = l.Log.ElapsedMs,
            StateBefore = l.Log.StateBefore.ToString(),
            RungBefore = l.Log.RungBefore,
            RungAfter = l.Log.RungAfter,
            IntervalBeforeDays = l.Log.IntervalBeforeDays,
            IntervalAfterDays = l.Log.IntervalAfterDays,
            VoidedAtUtc = l.Log.VoidedAtUtc,
            VoidReason = l.Log.VoidReason
        }).ToList();
    }
}
