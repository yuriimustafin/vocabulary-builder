using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Lists.Commands;

public record UpdateListCommand : IRequest<bool>
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public ListStatus Status { get; init; }
}

public class UpdateListCommandHandler : IRequestHandler<UpdateListCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateListCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateListCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.VocabularyLists.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        if (entity.Title != request.Title || entity.Status != request.Status)
        {
            _context.RecordActivity(
                ActivityAction.ListUpdated,
                language: entity.Language,
                listId: entity.Id,
                summary: entity.Title != request.Title
                    ? $"\"{entity.Title}\" renamed to \"{request.Title}\""
                    : $"\"{entity.Title}\": {entity.Status} → {request.Status}",
                details: new
                {
                    title = new { from = entity.Title, to = request.Title },
                    status = new { from = entity.Status, to = request.Status }
                });
        }

        entity.Title = request.Title;
        entity.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
