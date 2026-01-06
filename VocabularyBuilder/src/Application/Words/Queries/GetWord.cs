using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Queries;

public record GetWordQuery(int Id) : IRequest<WordDto?>;

public class GetWordQueryHandler : IRequestHandler<GetWordQuery, WordDto?>
{
    private readonly IApplicationDbContext _context;

    public GetWordQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WordDto?> Handle(GetWordQuery request, CancellationToken cancellationToken)
    {
        var word = await _context.Words
            .Include(w => w.WordEncounters)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

        if (word == null)
            return null;

        return new WordDto
        {
            Id = word.Id,
            Headword = word.Headword,
            Transcription = word.Transcription,
            PartOfSpeech = word.PartOfSpeech,
            Frequency = word.Frequency,
            EncounterCount = word.WordEncounters?.Count ?? 0,
            Examples = word.Examples?.ToList() ?? new List<string>()
        };
    }
}
