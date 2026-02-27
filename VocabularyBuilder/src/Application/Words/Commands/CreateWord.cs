using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public record CreateWordCommand : IRequest<int>
{
    public string Headword { get; init; } = string.Empty;    public Language Language { get; init; } = Language.English;    public string? Transcription { get; init; }
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
    private readonly ISender _sender;

    public CreateWordCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<int> Handle(CreateWordCommand request, CancellationToken cancellationToken)
    {
        // Look up frequency if not provided
        var frequency = request.Frequency;
        if (!frequency.HasValue)
        {
            frequency = await _sender.Send(new GetWordFrequencyQuery(request.Headword, request.Language), cancellationToken);
        }

        var entity = new Word
        {
            Headword = request.Headword,
            Language = request.Language,
            Transcription = request.Transcription,
            PartOfSpeech = request.PartOfSpeech,
            Frequency = frequency,
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
