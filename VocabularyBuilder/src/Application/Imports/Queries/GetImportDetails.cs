using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Imports.Queries;

public class ImportItemDto
{
    public int Id { get; set; }

    /// <summary>Null once the word has been deleted - the headword still says what it was.</summary>
    public int? WordId { get; set; }

    public string Headword { get; set; } = string.Empty;
    public string SourceTerm { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public bool EncounterAdded { get; set; }
    public string? Reason { get; set; }

    /// <summary>The word's status now, not at the time of the import.</summary>
    public WordStatus? CurrentStatus { get; set; }
}

public class ImportCallsSummaryDto
{
    public int Calls { get; set; }
    public int Failed { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}

public class ImportDetailsDto : ImportDto
{
    public List<ImportItemDto> Items { get; set; } = new();

    /// <summary>The model and dictionary requests made while the import ran.</summary>
    public ImportCallsSummaryDto ExternalCalls { get; set; } = new();
}

public record GetImportDetailsQuery(int Id) : IRequest<ImportDetailsDto?>;

public class GetImportDetailsQueryHandler : IRequestHandler<GetImportDetailsQuery, ImportDetailsDto?>
{
    private readonly IApplicationDbContext _context;

    public GetImportDetailsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ImportDetailsDto?> Handle(GetImportDetailsQuery request, CancellationToken cancellationToken)
    {
        var import = await _context.VocabularyImports
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (import is null)
        {
            return null;
        }

        // In the order the source listed them, which is the order they were written
        var items = await _context.VocabularyImportItems
            .AsNoTracking()
            .Where(i => i.ImportId == import.Id)
            .OrderBy(i => i.Id)
            .Select(i => new ImportItemDto
            {
                Id = i.Id,
                WordId = i.WordId,
                Headword = i.Headword,
                SourceTerm = i.SourceTerm,
                Outcome = i.Outcome.ToString(),
                EncounterAdded = i.EncounterAdded,
                Reason = i.Reason,
                CurrentStatus = i.Word != null ? i.Word.Status : null
            })
            .ToListAsync(cancellationToken);

        var calls = await _context.ExternalCallLog
            .AsNoTracking()
            .Where(c => c.ImportId == import.Id)
            .Select(c => new { c.Succeeded, c.PromptTokens, c.CompletionTokens })
            .ToListAsync(cancellationToken);

        var details = GetImportsQueryHandler.Fill(new ImportDetailsDto(), import);

        details.Items = items;
        details.ExternalCalls = new ImportCallsSummaryDto
        {
            Calls = calls.Count,
            Failed = calls.Count(c => !c.Succeeded),
            PromptTokens = calls.Sum(c => c.PromptTokens ?? 0),
            CompletionTokens = calls.Sum(c => c.CompletionTokens ?? 0)
        };

        return details;
    }
}
