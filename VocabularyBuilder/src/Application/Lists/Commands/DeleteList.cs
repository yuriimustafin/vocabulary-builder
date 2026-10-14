using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Lists.Commands;

public record DeleteListCommand(int Id) : IRequest<bool>;

public class DeleteListCommandHandler : IRequestHandler<DeleteListCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public DeleteListCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(DeleteListCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.VocabularyLists.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        _context.RecordActivity(
            ActivityAction.ListDeleted,
            language: entity.Language,
            listId: entity.Id,
            summary: $"\"{entity.Title}\" deleted");

        _context.VocabularyLists.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
