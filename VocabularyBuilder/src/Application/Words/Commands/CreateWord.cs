using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public record CreateWordCommand : IRequest<int>
{
    public string Headword { get; init; } = string.Empty;
    public string? Transcription { get; init; }
    public string? PartOfSpeech { get; init; }
    public int? Frequency { get; init; }
    public int EncounterCount { get; init; }
    public List<string>? Examples { get; init; }
}

public class CreateWordCommandHandler : IRequestHandler<CreateWordCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateWordCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateWordCommand request, CancellationToken cancellationToken)
    {
        var entity = new Word
        {
            Headword = request.Headword,
            Transcription = request.Transcription,
            PartOfSpeech = request.PartOfSpeech,
            Frequency = request.Frequency,
            EncounterCount = request.EncounterCount,
            Examples = request.Examples
        };

        _context.Words.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
