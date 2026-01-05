using VocabularyBuilder.Application.Common.Interfaces;
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

        _context.Words.Remove(entity);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
