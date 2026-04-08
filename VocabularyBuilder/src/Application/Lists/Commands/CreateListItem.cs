using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Lists.Commands;

public record CreateListItemCommand : IRequest<int>
{
    public int ListId { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool IsMastered { get; init; } = false;
}

public class CreateListItemCommandHandler : IRequestHandler<CreateListItemCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateListItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateListItemCommand request, CancellationToken cancellationToken)
    {
        // Verify the list exists
        var list = await _context.VocabularyLists.FindAsync(new object[] { request.ListId }, cancellationToken);
        Guard.Against.NotFound(request.ListId, list);

        var entity = new VocabularyListItem
        {
            ListId = request.ListId,
            Text = request.Text,
            IsMastered = request.IsMastered
        };

        _context.VocabularyListItems.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
