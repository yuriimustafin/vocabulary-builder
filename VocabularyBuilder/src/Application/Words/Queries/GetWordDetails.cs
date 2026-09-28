using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
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

        // Loaded separately: WordForms has no navigation from Word, and a verb carries
        // around eighty of them. Id order is the order the conjugation page listed them in.
        var forms = await _context.WordForms
            .AsNoTracking()
            .Where(wf => wf.WordId == word.Id)
            .OrderBy(wf => wf.Id)
            .Select(wf => new WordFormDto
            {
                Form = wf.Form,
                Mood = wf.Mood,
                Tense = wf.Tense,
                Person = wf.Person
            })
            .ToListAsync(cancellationToken);

        return new WordDetailsDto
        {
            Id = word.Id,
            Headword = word.Headword,
            Transcription = word.Transcription,
            PartOfSpeech = word.PartOfSpeech,
            Gender = word.Gender,
            IsPluralOnly = word.IsPluralOnly,
            Article = NounArticleDto.From(word.GetArticle()),
            Forms = forms,
            Frequency = word.Frequency,
            Status = word.Status,
            Language = word.Language,
            Examples = word.Examples?.ToList() ?? new List<string>(),
            Tags = word.Tags?.ToList() ?? new List<string>(),
            Senses = word.Senses?.OrderBy(s => s.Id).Select(s => new SenseDto
            {
                Definition = s.Definition,
                PartOfSpeech = s.PartOfSpeech.ToString(),
                Article = NounArticleDto.From(word.GetArticle(s)),
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
    public GrammaticalGender? Gender { get; set; }
    public bool IsPluralOnly { get; set; }
    public NounArticleDto? Article { get; set; }
    public int? Frequency { get; set; }
    public WordStatus Status { get; set; }
    public Language Language { get; set; }
    public List<string> Examples { get; set; } = new();

    /// <summary>Labels the word has been collected under.</summary>
    public List<string> Tags { get; set; } = new();

    public List<SenseDto> Senses { get; set; } = new();

    /// <summary>Inflected forms, in the order the source listed them. Empty for most non-verbs.</summary>
    public List<WordFormDto> Forms { get; set; } = new();
    public List<EncounterDto> Encounters { get; set; } = new();
    public List<DictionarySourceDto> DictionarySources { get; set; } = new();
}

public class SenseDto
{
    public string Definition { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;

    /// <summary>The article for this meaning, which can differ from the word's ("la livre").</summary>
    public NounArticleDto? Article { get; set; }

    public List<string> Examples { get; set; } = new();
}

public class WordFormDto
{
    public string Form { get; set; } = string.Empty;
    public string? Mood { get; set; }
    public string? Tense { get; set; }
    public string? Person { get; set; }
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
