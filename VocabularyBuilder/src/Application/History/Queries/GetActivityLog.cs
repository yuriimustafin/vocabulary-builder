using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.History.Queries;

public class ActivityLogEntryDto
{
    public int Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Action { get; set; } = string.Empty;

    /// <summary>Words, Dictionary, Imports, Study or Lists - what the page filters by.</summary>
    public string Category { get; set; } = string.Empty;

    public Language? Language { get; set; }
    public int? WordId { get; set; }
    public string? Headword { get; set; }
    public int? ListId { get; set; }
    public int? ImportId { get; set; }
    public string? Summary { get; set; }
    public string? Details { get; set; }

    internal static ActivityLogEntryDto From(ActivityLogEntry entry) => new()
    {
        Id = entry.Id,
        OccurredAtUtc = entry.OccurredAtUtc,
        Action = entry.Action.ToString(),
        Category = ActivityCategories.Of(entry.Action),
        Language = entry.Language,
        WordId = entry.WordId,
        Headword = entry.Headword,
        ListId = entry.ListId,
        ImportId = entry.ImportId,
        Summary = entry.Summary,
        Details = entry.Details
    };
}

/// <summary>Groups the actions the way the history page offers them as filters.</summary>
public static class ActivityCategories
{
    public const string Words = "Words";
    public const string Dictionary = "Dictionary";
    public const string Imports = "Imports";
    public const string Study = "Study";
    public const string Lists = "Lists";

    public static string Of(ActivityAction action) => action switch
    {
        ActivityAction.WordFilledFromDictionary or ActivityAction.WordNotFoundInDictionary
            or ActivityAction.DictionaryFillRun or ActivityAction.CachedSensesReparsed
            or ActivityAction.FrequenciesUpdated => Dictionary,

        ActivityAction.ImportCompleted or ActivityAction.ImportFailed
            or ActivityAction.FrequencyDataImported => Imports,

        ActivityAction.CardSuspended or ActivityAction.CardResumed or ActivityAction.CardReset
            or ActivityAction.StudyProgressCleared or ActivityAction.WordMarkedForStudy
            or ActivityAction.WordUnmarkedForStudy or ActivityAction.WordMarkedKnown => Study,

        ActivityAction.ListCreated or ActivityAction.ListUpdated or ActivityAction.ListDeleted
            or ActivityAction.ListItemAdded or ActivityAction.ListItemUpdated
            or ActivityAction.ListItemDeleted => Lists,

        _ => Words
    };

    public static IReadOnlyList<ActivityAction> In(string category) =>
        Enum.GetValues<ActivityAction>().Where(a => Of(a) == category).ToList();
}

/// <summary>
/// The activity log, newest first. An entry with no language - a list item, a sweep over
/// everything - is shown under both.
/// </summary>
public record GetActivityLogQuery : IRequest<PaginatedList<ActivityLogEntryDto>>
{
    public Language Language { get; init; }
    public string? Category { get; init; }
    public int? WordId { get; init; }
    public int? ImportId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetActivityLogQueryHandler : IRequestHandler<GetActivityLogQuery, PaginatedList<ActivityLogEntryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetActivityLogQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ActivityLogEntryDto>> Handle(GetActivityLogQuery request, CancellationToken cancellationToken)
    {
        var entries = _context.ActivityLog
            .AsNoTracking()
            .Where(e => e.Language == request.Language || e.Language == null);

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var actions = ActivityCategories.In(request.Category);
            entries = entries.Where(e => actions.Contains(e.Action));
        }

        if (request.WordId.HasValue)
        {
            entries = entries.Where(e => e.WordId == request.WordId);
        }

        if (request.ImportId.HasValue)
        {
            entries = entries.Where(e => e.ImportId == request.ImportId);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var count = await entries.CountAsync(cancellationToken);

        // By id: the log is append-only, so id order is time order, and SQLite can sort by it
        var page = await entries
            .OrderByDescending(e => e.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<ActivityLogEntryDto>(
            page.Select(ActivityLogEntryDto.From).ToList(), count, pageNumber, pageSize);
    }
}
