using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Lists.Commands;

public record CreateListCommand : IRequest<int>
{
    public string Title { get; init; } = string.Empty;
    public Language Language { get; init; } = Language.English;
    public ListStatus Status { get; init; } = ListStatus.Active;
    public List<string>? Items { get; init; }
}

public class CreateListCommandHandler : IRequestHandler<CreateListCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateListCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateListCommand request, CancellationToken cancellationToken)
    {
        var entity = new VocabularyList
        {
            Title = request.Title,
            Language = request.Language,
            Status = request.Status
        };

        // Add items if provided
        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var itemText in request.Items)
            {
                entity.Items.Add(new VocabularyListItem
                {
                    Text = itemText.Trim(),
                    IsMastered = false
                });
            }
        }

        _context.VocabularyLists.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
