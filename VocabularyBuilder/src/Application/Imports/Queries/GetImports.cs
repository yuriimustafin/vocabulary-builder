using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Domain.Entities.Imports;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Imports.Queries;

public class ImportDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Language Language { get; set; }
    public string? Name { get; set; }
    public string? FileName { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Error { get; set; }
    public bool IsReconstructed { get; set; }
    public int TermsRead { get; set; }
    public int WordsCreated { get; set; }
    public int WordsTouched { get; set; }
    public int EncountersCreated { get; set; }
    public int TermsSkipped { get; set; }
}

/// <summary>A language's imports, newest first.</summary>
public record GetImportsQuery(Language Language, int PageNumber = 1, int PageSize = 20)
    : IRequest<PaginatedList<ImportDto>>;

public class GetImportsQueryHandler : IRequestHandler<GetImportsQuery, PaginatedList<ImportDto>>
{
    private readonly IApplicationDbContext _context;

    public GetImportsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ImportDto>> Handle(GetImportsQuery request, CancellationToken cancellationToken)
    {
        var imports = _context.VocabularyImports
            .AsNoTracking()
            .Where(i => i.Language == request.Language);

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var count = await imports.CountAsync(cancellationToken);

        var page = await imports
            .OrderByDescending(i => i.StartedAtUtc)
            .ThenByDescending(i => i.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<ImportDto>(page.Select(ToDto).ToList(), count, pageNumber, pageSize);
    }

    internal static ImportDto ToDto(VocabularyImport import) => Fill(new ImportDto(), import);

    internal static T Fill<T>(T dto, VocabularyImport import) where T : ImportDto
    {
        dto.Id = import.Id;
        dto.Kind = import.Kind.ToString();
        dto.Language = import.Language;
        dto.Name = import.Name;
        dto.FileName = import.FileName;
        dto.Tags = import.Tags?.ToList() ?? new List<string>();
        dto.Status = import.Status.ToString();
        dto.StartedAtUtc = import.StartedAtUtc;
        dto.CompletedAtUtc = import.CompletedAtUtc;
        dto.Error = import.Error;
        dto.IsReconstructed = import.IsReconstructed;
        dto.TermsRead = import.TermsRead;
        dto.WordsCreated = import.WordsCreated;
        dto.WordsTouched = import.WordsTouched;
        dto.EncountersCreated = import.EncountersCreated;
        dto.TermsSkipped = import.TermsSkipped;
        return dto;
    }
}
