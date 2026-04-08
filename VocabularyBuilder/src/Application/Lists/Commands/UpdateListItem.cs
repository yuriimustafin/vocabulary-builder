using VocabularyBuilder.Application.Common.Interfaces;

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

        entity.Text = request.Text;
        entity.IsMastered = request.IsMastered;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
