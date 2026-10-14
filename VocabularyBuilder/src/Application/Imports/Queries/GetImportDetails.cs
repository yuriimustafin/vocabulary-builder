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

    /// <summary>The form the word was met in, when the source gave one.</summary>
    public string? Form { get; set; }

    /// <summary>The sentence it was met in became a practice example.</summary>
    public bool ExampleAdded { get; set; }

    /// <summary>The form was new, so the word's study content will be generated again.</summary>
    public bool ContentReopened { get; set; }

    /// <summary>The word's status now, not at the time of the import.</summary>
    public WordStatus? CurrentStatus { get; set; }
}

public class ImportCallsSummaryDto
{
    public int Calls { get; set; }

    /// <summary>
    /// Of those, the study-content calls made later for words this import reopened - the
    /// cost of the new forms it brought in, paid in a study session after it finished.
    /// </summary>
    public int LaterStudyContentCalls { get; set; }

    public int Failed { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
}

public class ImportDetailsDto : ImportDto
{
    public List<ImportItemDto> Items { get; set; } = new();

    public int ExamplesAdded { get; set; }

    public int ContentReopened { get; set; }

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
                Form = i.Form,
                ExampleAdded = i.ExampleAdded,
                ContentReopened = i.ContentReopened,
                CurrentStatus = i.Word != null ? i.Word.Status : null
            })
            .ToListAsync(cancellationToken);

        var calls = await _context.ExternalCallLog
            .AsNoTracking()
            .Where(c => c.ImportId == import.Id)
            .Select(c => new { c.Succeeded, c.PromptTokens, c.CompletionTokens, c.Purpose, c.StartedAtUtc })
            .ToListAsync(cancellationToken);

        var details = GetImportsQueryHandler.Fill(new ImportDetailsDto(), import);

        details.Items = items;
        details.ExamplesAdded = items.Count(i => i.ExampleAdded);
        details.ContentReopened = items.Count(i => i.ContentReopened);
        details.ExternalCalls = new ImportCallsSummaryDto
        {
            Calls = calls.Count,
            LaterStudyContentCalls = calls.Count(c =>
                c.Purpose == ExternalCallPurpose.StudyContent
                && (import.CompletedAtUtc is null || c.StartedAtUtc > import.CompletedAtUtc)),
            Failed = calls.Count(c => !c.Succeeded),
            PromptTokens = calls.Sum(c => c.PromptTokens ?? 0),
            CompletionTokens = calls.Sum(c => c.CompletionTokens ?? 0)
        };

        return details;
    }
}
