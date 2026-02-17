using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Queries;

public record GetWordsForExportQuery(
    List<WordStatus>? Statuses = null) : IRequest<List<Word>>;

public class GetWordsForExportQueryHandler : IRequestHandler<GetWordsForExportQuery, List<Word>>
{
    private readonly IApplicationDbContext _context;

    public GetWordsForExportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Word>> Handle(GetWordsForExportQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Words
            .Include(w => w.Senses!)
            .AsNoTracking();

        // Apply status filter - default to NextExport status
        if (request.Statuses != null && request.Statuses.Any())
        {
            query = query.Where(w => request.Statuses.Contains(w.Status));
        }
        else
        {
            // Default: only export words with NextExport status
            query = query.Where(w => w.Status == WordStatus.NextExport);
        }

        var words = await query
            .OrderBy(w => w.Headword)
            .ToListAsync(cancellationToken);

        return words;
    }
}
