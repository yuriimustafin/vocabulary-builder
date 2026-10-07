using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.History.Queries;

/// <summary>A call as the list shows it - everything but the prompt and the answer.</summary>
public class ExternalCallDto
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public int DurationMs { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Target { get; set; }
    public string? Url { get; set; }
    public int? StatusCode { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public int? ResponseLength { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public bool IsMock { get; set; }
    public int? WordId { get; set; }
    public int? ImportId { get; set; }

    /// <summary>Whether the prompt and answer were kept, and can be opened.</summary>
    public bool HasBody { get; set; }

    internal static T Fill<T>(T dto, ExternalCallLog call) where T : ExternalCallDto
    {
        dto.Id = call.Id;
        dto.StartedAtUtc = call.StartedAtUtc;
        dto.DurationMs = call.DurationMs;
        dto.Provider = call.Provider.ToString();
        dto.Purpose = call.Purpose.ToString();
        dto.Model = call.Model;
        dto.Target = call.Target;
        dto.Url = call.Url;
        dto.StatusCode = call.StatusCode;
        dto.Succeeded = call.Succeeded;
        dto.Error = call.Error;
        dto.ResponseLength = call.ResponseLength;
        dto.PromptTokens = call.PromptTokens;
        dto.CompletionTokens = call.CompletionTokens;
        dto.IsMock = call.IsMock;
        dto.WordId = call.WordId;
        dto.ImportId = call.ImportId;
        dto.HasBody = call.Request != null || call.Response != null;
        return dto;
    }
}

public class ExternalCallDetailsDto : ExternalCallDto
{
    public string? Request { get; set; }
    public string? Response { get; set; }
}

/// <summary>
/// Outbound calls, newest first. Not split by language: the log does not know one, and a
/// study-content call is about a word of either.
/// </summary>
public record GetExternalCallsQuery : IRequest<PaginatedList<ExternalCallDto>>
{
    public ExternalCallProvider? Provider { get; init; }
    public ExternalCallPurpose? Purpose { get; init; }
    public bool FailedOnly { get; init; }
    public int? WordId { get; init; }
    public int? ImportId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetExternalCallsQueryHandler : IRequestHandler<GetExternalCallsQuery, PaginatedList<ExternalCallDto>>
{
    private readonly IApplicationDbContext _context;

    public GetExternalCallsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ExternalCallDto>> Handle(GetExternalCallsQuery request, CancellationToken cancellationToken)
    {
        var calls = _context.ExternalCallLog.AsNoTracking();

        if (request.Provider.HasValue)
        {
            calls = calls.Where(c => c.Provider == request.Provider);
        }

        if (request.Purpose.HasValue)
        {
            calls = calls.Where(c => c.Purpose == request.Purpose);
        }

        if (request.FailedOnly)
        {
            calls = calls.Where(c => !c.Succeeded);
        }

        if (request.WordId.HasValue)
        {
            calls = calls.Where(c => c.WordId == request.WordId);
        }

        if (request.ImportId.HasValue)
        {
            calls = calls.Where(c => c.ImportId == request.ImportId);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var count = await calls.CountAsync(cancellationToken);

        // Projected without the bodies, which run to kilobytes each and are opened one at a time
        var page = await calls
            .OrderByDescending(c => c.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ExternalCallLog
            {
                Id = c.Id,
                StartedAtUtc = c.StartedAtUtc,
                DurationMs = c.DurationMs,
                Provider = c.Provider,
                Purpose = c.Purpose,
                Model = c.Model,
                Target = c.Target,
                Url = c.Url,
                StatusCode = c.StatusCode,
                Succeeded = c.Succeeded,
                Error = c.Error,
                ResponseLength = c.ResponseLength,
                PromptTokens = c.PromptTokens,
                CompletionTokens = c.CompletionTokens,
                IsMock = c.IsMock,
                WordId = c.WordId,
                ImportId = c.ImportId,
                // Only whether there is one, not the thing itself
                Request = c.Request != null ? "" : null
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<ExternalCallDto>(
            page.Select(c => ExternalCallDto.Fill(new ExternalCallDto(), c)).ToList(), count, pageNumber, pageSize);
    }
}

public record GetExternalCallQuery(int Id) : IRequest<ExternalCallDetailsDto?>;

public class GetExternalCallQueryHandler : IRequestHandler<GetExternalCallQuery, ExternalCallDetailsDto?>
{
    private readonly IApplicationDbContext _context;

    public GetExternalCallQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExternalCallDetailsDto?> Handle(GetExternalCallQuery request, CancellationToken cancellationToken)
    {
        var call = await _context.ExternalCallLog
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (call is null)
        {
            return null;
        }

        var details = ExternalCallDto.Fill(new ExternalCallDetailsDto(), call);
        details.Request = call.Request;
        details.Response = call.Response;
        return details;
    }
}
