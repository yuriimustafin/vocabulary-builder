using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.History.Queries;

/// <summary>One import a word came in through, and what that import made of it.</summary>
public class WordImportDto
{
    public int ImportId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public bool IsReconstructed { get; set; }

    /// <summary>The terms of that import that landed on this word - "une randonnée", "la randonnée".</summary>
    public List<string> SourceTerms { get; set; } = new();

    /// <summary>Whether that import is the one that created the word.</summary>
    public bool CreatedWord { get; set; }
}

public class WordHistoryDto
{
    public List<WordImportDto> Imports { get; set; } = new();
    public List<ActivityLogEntryDto> Activity { get; set; } = new();
    public List<ExternalCallDto> Calls { get; set; } = new();

    /// <summary>Its graded answers, newest first - voided ones included, and marked.</summary>
    public List<ReviewLogEntryDto> Reviews { get; set; } = new();
}

/// <summary>
/// Everything recorded about one word: the imports it came in through, what was done to it,
/// and the model and dictionary calls made about it.
/// </summary>
/// <remarks>
/// A call made while a word was being imported was made before the word existed, so it has no
/// word id - only the word it was about. Those are matched by headword, within the imports the
/// word came in through, which is the one place that match cannot pick up a namesake.
/// </remarks>
public record GetWordHistoryQuery(int WordId) : IRequest<WordHistoryDto?>;

public class GetWordHistoryQueryHandler : IRequestHandler<GetWordHistoryQuery, WordHistoryDto?>
{
    private const int Limit = 100;

    private readonly IApplicationDbContext _context;

    public GetWordHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WordHistoryDto?> Handle(GetWordHistoryQuery request, CancellationToken cancellationToken)
    {
        var word = await _context.Words
            .AsNoTracking()
            .Where(w => w.Id == request.WordId)
            .Select(w => new { w.Id, w.Headword })
            .FirstOrDefaultAsync(cancellationToken);

        if (word is null)
        {
            return null;
        }

        var items = await _context.VocabularyImportItems
            .AsNoTracking()
            .Where(i => i.WordId == word.Id)
            .Select(i => new
            {
                i.ImportId,
                i.SourceTerm,
                i.Outcome,
                i.Import.Kind,
                i.Import.Name,
                i.Import.StartedAtUtc,
                i.Import.IsReconstructed
            })
            .ToListAsync(cancellationToken);

        var imports = items
            .GroupBy(i => i.ImportId)
            .Select(g => new WordImportDto
            {
                ImportId = g.Key,
                Kind = g.First().Kind.ToString(),
                Name = g.First().Name,
                StartedAtUtc = g.First().StartedAtUtc,
                IsReconstructed = g.First().IsReconstructed,
                SourceTerms = g.Select(i => i.SourceTerm).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                CreatedWord = g.Any(i => i.Outcome == ImportItemOutcome.Created)
            })
            .OrderByDescending(i => i.StartedAtUtc)
            .ToList();

        var activity = await _context.ActivityLog
            .AsNoTracking()
            .Where(e => e.WordId == word.Id)
            .OrderByDescending(e => e.Id)
            .Take(Limit)
            .ToListAsync(cancellationToken);

        var importIds = imports.Select(i => i.ImportId).ToList();

        var calls = await _context.ExternalCallLog
            .AsNoTracking()
            .Where(c => c.WordId == word.Id ||
                        (c.WordId == null && c.ImportId != null && importIds.Contains(c.ImportId.Value) &&
                         c.Target == word.Headword))
            .OrderByDescending(c => c.Id)
            .Take(Limit)
            .ToListAsync(cancellationToken);

        var reviews = await _context.ReviewLogs
            .AsNoTracking()
            .Where(l => l.WordId == word.Id && !l.IsScaffold)
            .OrderByDescending(l => l.Id)
            .Take(Limit)
            .ToListAsync(cancellationToken);

        return new WordHistoryDto
        {
            Imports = imports,
            Activity = activity.Select(ActivityLogEntryDto.From).ToList(),
            Calls = calls.Select(c => ExternalCallDto.Fill(new ExternalCallDto(), c)).ToList(),
            Reviews = await new GetReviewLogQueryHandler(_context).WithSentences(
                reviews.Select(r => (r, word.Headword)).ToList(), cancellationToken)
        };
    }
}
