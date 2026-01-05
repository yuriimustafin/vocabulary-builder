using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public record UpdateWordCommand : IRequest
{
    public int Id { get; init; }
    public string Headword { get; init; } = string.Empty;
    public string? Transcription { get; init; }
    public string? PartOfSpeech { get; init; }
    public int? Frequency { get; init; }
    public int EncounterCount { get; init; }
    public List<string>? Examples { get; init; }
}

public class UpdateWordCommandHandler : IRequestHandler<UpdateWordCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateWordCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateWordCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Words
            .FindAsync(new object[] { request.Id }, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.Headword = request.Headword;
        entity.Transcription = request.Transcription;
        entity.PartOfSpeech = request.PartOfSpeech;
        entity.Frequency = request.Frequency;
        entity.EncounterCount = request.EncounterCount;
        entity.Examples = request.Examples;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
