using VocabularyBuilder.Application.Common.Interfaces;

namespace VocabularyBuilder.Application.Lists.Commands;

public record DeleteListItemCommand(int Id) : IRequest<bool>;

public class DeleteListItemCommandHandler : IRequestHandler<DeleteListItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteListItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteListItemCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.VocabularyListItems.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        _context.VocabularyListItems.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
