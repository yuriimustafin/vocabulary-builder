using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Lists.Queries;

public record GetListDetailsQuery(int Id) : IRequest<VocabularyListDetailsDto?>;

public class VocabularyListDetailsDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public Language Language { get; init; }
    public ListStatus Status { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public List<VocabularyListItemDto> Items { get; init; } = new();
}

public class VocabularyListItemDto
{
    public int Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool IsMastered { get; init; }
    public DateTimeOffset Created { get; init; }
}

public class GetListDetailsQueryHandler : IRequestHandler<GetListDetailsQuery, VocabularyListDetailsDto?>
{
    private readonly IApplicationDbContext _context;

    public GetListDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VocabularyListDetailsDto?> Handle(GetListDetailsQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.VocabularyLists
            .Include(l => l.Items)
            .Where(l => l.Id == request.Id)
            .Select(l => new VocabularyListDetailsDto
            {
                Id = l.Id,
                Title = l.Title,
                Language = l.Language,
                Status = l.Status,
                Created = l.Created,
                LastModified = l.LastModified,
                Items = l.Items
                    .OrderBy(i => i.Text)
                    .Select(i => new VocabularyListItemDto
                    {
                        Id = i.Id,
                        Text = i.Text,
                        IsMastered = i.IsMastered,
                        Created = i.Created
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return list;
    }
}
