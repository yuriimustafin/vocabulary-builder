using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Lists.Commands;

public record UpdateListItemCommand : IRequest<bool>
{
    public int Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool IsMastered { get; init; }
}

public class UpdateListItemCommandHandler : IRequestHandler<UpdateListItemCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateListItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateListItemCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.VocabularyListItems.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity == null)
        {
            return false;
        }

        if (entity.Text != request.Text || entity.IsMastered != request.IsMastered)
        {
            _context.RecordActivity(
                ActivityAction.ListItemUpdated,
                listId: entity.ListId,
                summary: entity.Text != request.Text
                    ? $"\"{entity.Text}\" changed to \"{request.Text}\""
                    : $"\"{entity.Text}\" {(request.IsMastered ? "mastered" : "no longer mastered")}",
                details: new
                {
                    text = new { from = entity.Text, to = request.Text },
                    isMastered = new { from = entity.IsMastered, to = request.IsMastered }
                });
        }

        entity.Text = request.Text;
        entity.IsMastered = request.IsMastered;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
