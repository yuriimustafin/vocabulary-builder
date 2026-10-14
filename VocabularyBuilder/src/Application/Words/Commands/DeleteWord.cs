using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public record DeleteWordCommand(int Id) : IRequest;

public class DeleteWordCommandHandler : IRequestHandler<DeleteWordCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteWordCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteWordCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Words
            .FindAsync(new object[] { request.Id }, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        // The word takes its encounters, cached pages and study history with it; this entry
        // is what is left to say it was ever there
        _context.RecordActivity(
            ActivityAction.WordDeleted,
            entity,
            summary: $"Deleted ({entity.Status})",
            details: new { entity.Status, entity.PartOfSpeech, entity.Gender, entity.Tags });

        _context.Words.Remove(entity);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
