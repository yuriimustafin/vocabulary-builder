using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Queries;

public record GetWordDetailsQuery(int Id) : IRequest<WordDetailsDto?>;

public class GetWordDetailsQueryHandler : IRequestHandler<GetWordDetailsQuery, WordDetailsDto?>
{
    private readonly IApplicationDbContext _context;

    public GetWordDetailsQueryHandler(IApplicationDbContext _context)
    {
        this._context = _context;
    }

    public async Task<WordDetailsDto?> Handle(GetWordDetailsQuery request, CancellationToken cancellationToken)
    {
        var word = await _context.Words
            .Include(w => w.Senses)
            .Include(w => w.WordEncounters)
            .Include(w => w.DictionarySources)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

        if (word == null)
            return null;

        return new WordDetailsDto
        {
            Id = word.Id,
            Headword = word.Headword,
            Transcription = word.Transcription,
            PartOfSpeech = word.PartOfSpeech,
            Frequency = word.Frequency,
            Status = word.Status,
            Language = word.Language,
            Examples = word.Examples?.ToList() ?? new List<string>(),
            Senses = word.Senses?.Select(s => new SenseDto
            {
                Definition = s.Definition,
                PartOfSpeech = s.PartOfSpeech.ToString(),
                Examples = s.Examples?.ToList() ?? new List<string>()
            }).ToList() ?? new List<SenseDto>(),
            Encounters = word.WordEncounters?.Select(e => new EncounterDto
            {
                Source = e.Source.ToString(),
                SourceIdentifier = e.SourceIdentifier,
                Context = e.Context,
                Notes = e.Notes,
                EncounteredAt = e.Created
            }).OrderByDescending(e => e.EncounteredAt).ToList() ?? new List<EncounterDto>(),
            DictionarySources = word.DictionarySources?.Select(ds => new DictionarySourceDto
            {
                SourceType = ds.SourceType.ToString(),
                SourceUrl = ds.SourceUrl
            }).ToList() ?? new List<DictionarySourceDto>()
        };
    }
}

public class WordDetailsDto
{
    public int Id { get; set; }
    public string Headword { get; set; } = string.Empty;
    public string? Transcription { get; set; }
    public string? PartOfSpeech { get; set; }
    public int? Frequency { get; set; }
    public WordStatus Status { get; set; }
    public Language Language { get; set; }
    public List<string> Examples { get; set; } = new();
    public List<SenseDto> Senses { get; set; } = new();
    public List<EncounterDto> Encounters { get; set; } = new();
    public List<DictionarySourceDto> DictionarySources { get; set; } = new();
}

public class SenseDto
{
    public string Definition { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();
}

public class EncounterDto
{
    public string Source { get; set; } = string.Empty;
    public string? SourceIdentifier { get; set; }
    public string? Context { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset EncounteredAt { get; set; }
}

public class DictionarySourceDto
{
    public string SourceType { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
}
