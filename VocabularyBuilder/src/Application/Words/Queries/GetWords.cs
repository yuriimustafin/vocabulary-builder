using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Queries;

public record GetWordsQuery(
    string? SortBy = null,
    List<WordStatus>? Statuses = null,
    int? MinEncounterCount = null,
    int? MaxEncounterCount = null) : IRequest<List<WordDto>>;

public class GetWordsQueryHandler : IRequestHandler<GetWordsQuery, List<WordDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWordsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WordDto>> Handle(GetWordsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Words
            .Include(w => w.WordEncounters)
            .AsNoTracking();

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

        return words.Select(w => new WordDto
        {
            Id = w.Id,
            Headword = w.Headword,
            Transcription = w.Transcription,
            PartOfSpeech = w.PartOfSpeech,
            Frequency = w.Frequency,
            EncounterCount = w.WordEncounters?.Count ?? 0,
            Examples = w.Examples?.ToList() ?? new List<string>(),
            Status = w.Status
        }).ToList();
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
    public WordStatus Status { get; set; }
}
