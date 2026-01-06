using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Queries;

public record GetWordsQuery : IRequest<List<WordDto>>;

public class GetWordsQueryHandler : IRequestHandler<GetWordsQuery, List<WordDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWordsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WordDto>> Handle(GetWordsQuery request, CancellationToken cancellationToken)
    {
        var words = await _context.Words
            .Include(w => w.WordEncounters)
            .OrderBy(w => w.Headword)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return words.Select(w => new WordDto
        {
            Id = w.Id,
            Headword = w.Headword,
            Transcription = w.Transcription,
            PartOfSpeech = w.PartOfSpeech,
            Frequency = w.Frequency,
            EncounterCount = w.WordEncounters?.Count ?? 0,
            Examples = w.Examples?.ToList() ?? new List<string>()
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
}
