using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Queries;

public record GetWordsQuery(
    Language Language = Language.English,
    string? SortBy = null,
    List<WordStatus>? Statuses = null,
    int? MinEncounterCount = null,
    int? MaxEncounterCount = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PaginatedList<WordDto>>;

public class GetWordsQueryHandler : IRequestHandler<GetWordsQuery, PaginatedList<WordDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWordsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<WordDto>> Handle(GetWordsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Words
            .Include(w => w.WordEncounters)
            .AsNoTracking()
            .Where(w => w.Language == request.Language);

        // Apply status filter
        if (request.Statuses != null && request.Statuses.Any())
        {
            query = query.Where(w => request.Statuses.Contains(w.Status));
        }

        // Apply simple sorting that can be translated to SQL
        // Note: SQLite doesn't support DateTimeOffset in ORDER BY, so those sorts are done in-memory
        query = request.SortBy?.ToLower() switch
        {
            "frequency" => query.OrderByDescending(w => w.Frequency).ThenBy(w => w.Headword),
            _ => query.OrderBy(w => w.Headword) // Default: alphabetical
        };

        // Get total count before in-memory filtering
        var totalCount = await query.CountAsync(cancellationToken);
        var words = await query.ToListAsync(cancellationToken);

        // Apply encounter count filter in-memory
        if (request.MinEncounterCount.HasValue)
        {
            words = words.Where(w => (w.WordEncounters?.Count ?? 0) >= request.MinEncounterCount.Value).ToList();
        }
        if (request.MaxEncounterCount.HasValue)
        {
            words = words.Where(w => (w.WordEncounters?.Count ?? 0) <= request.MaxEncounterCount.Value).ToList();
        }

        // For date-based sorting, we need to do it in memory after loading (SQLite limitation)
        if (request.SortBy?.ToLower() == "lastencounter")
        {
            words = words
                .OrderByDescending(w => w.WordEncounters != null && w.WordEncounters.Any() 
                    ? w.WordEncounters.Max(e => e.Created) 
                    : DateTimeOffset.MinValue)
                .ThenBy(w => w.Headword)
                .ToList();
        }
        else if (request.SortBy?.ToLower() == "created")
        {
            words = words
                .OrderByDescending(w => w.Created)
                .ThenBy(w => w.Headword)
                .ToList();
        }
        else if (request.SortBy?.ToLower() == "encounterfrequency")
        {
            words = words
                .OrderByDescending(w => w.WordEncounters?.Count ?? 0)
                .ThenByDescending(w => w.Frequency ?? 0)
                .ThenBy(w => w.Headword)
                .ToList();
        }

        // Update total count after in-memory filtering
        totalCount = words.Count;

        // Apply pagination
        var pagedWords = words
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var wordDtos = pagedWords.Select(w => new WordDto
        {
            Id = w.Id,
            Headword = w.Headword,
            Transcription = w.Transcription,
            PartOfSpeech = w.PartOfSpeech,
            Frequency = w.Frequency,
            EncounterCount = w.WordEncounters?.Count ?? 0,
            Examples = w.Examples?.ToList() ?? new List<string>(),
            Status = w.Status,
            Language = w.Language
        }).ToList();

        return new PaginatedList<WordDto>(wordDtos, totalCount, request.PageNumber, request.PageSize);
    }
}

public class WordDto
{
    public int Id { get; set; }
    public string Headword { get; set; } = string.Empty;
    public string? Transcription { get; set; }
    public string? PartOfSpeech { get; set; }
    public int? Frequency { get; set; }
    public int EncounterCount { get; set; }
    public List<string> Examples { get; set; } = new();
    public Language Language { get; set; }
    public WordStatus Status { get; set; }
}
