using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public record CreateWordCommand : IRequest<int>
{
    public string Headword { get; init; } = string.Empty;
    public string? Transcription { get; init; }
    public string? PartOfSpeech { get; init; }
    public int? Frequency { get; init; }
    public List<string>? Examples { get; init; }
    
    // Properties for creating WordEncounter
    public WordEncounterSource Source { get; init; } = WordEncounterSource.Manual;
    public string? SourceIdentifier { get; init; }
    public string? Context { get; init; }
    public string? Notes { get; init; }
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
            Examples = request.Examples
        };

        _context.Words.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        // Create the initial encounter record
        var encounter = new WordEncounter
        {
            WordId = entity.Id,
            Source = request.Source,
            SourceIdentifier = request.SourceIdentifier,
            Context = request.Context,
            Notes = request.Notes
        };
        
        _context.WordEncounters.Add(encounter);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
