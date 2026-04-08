using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Lists.Queries;

public record GetListsQuery(Language Language) : IRequest<List<VocabularyListDto>>;

public class VocabularyListDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public Language Language { get; init; }
    public ListStatus Status { get; init; }
    public int ItemCount { get; init; }
    public int MasteredCount { get; init; }
    public DateTimeOffset Created { get; init; }
}

public class GetListsQueryHandler : IRequestHandler<GetListsQuery, List<VocabularyListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetListsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<VocabularyListDto>> Handle(GetListsQuery request, CancellationToken cancellationToken)
    {
        var lists = await _context.VocabularyLists
            .Include(l => l.Items)
            .Where(l => l.Language == request.Language)
            .OrderByDescending(l => l.Id) // Order by ID descending (newest first) - SQLite doesn't support DateTimeOffset in ORDER BY
            .Select(l => new VocabularyListDto
            {
                Id = l.Id,
                Title = l.Title,
                Language = l.Language,
                Status = l.Status,
                ItemCount = l.Items.Count,
                MasteredCount = l.Items.Count(i => i.IsMastered),
                Created = l.Created
            })
            .ToListAsync(cancellationToken);

        return lists;
    }
}
