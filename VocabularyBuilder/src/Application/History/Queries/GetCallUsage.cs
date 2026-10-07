using VocabularyBuilder.Application.Common.Interfaces;

namespace VocabularyBuilder.Application.History.Queries;

/// <summary>What one kind of call cost over the period.</summary>
public class CallUsageDto
{
    public string Provider { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;

    /// <summary>The prompt version, where the prompt has one - a bump re-asks every word.</summary>
    public string? PromptVersion { get; set; }

    public int Calls { get; set; }
    public int Failed { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public long TotalDurationMs { get; set; }
    public DateTime FirstAtUtc { get; set; }
    public DateTime LastAtUtc { get; set; }
}

/// <summary>
/// Outbound calls totalled by service, purpose and prompt version over the last few days -
/// the view that shows what a study-content version bump or a large import actually cost.
/// </summary>
/// <param name="Days">How far back to count; null for everything.</param>
public record GetCallUsageQuery(int? Days = 30) : IRequest<List<CallUsageDto>>;

public class GetCallUsageQueryHandler : IRequestHandler<GetCallUsageQuery, List<CallUsageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public GetCallUsageQueryHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<List<CallUsageDto>> Handle(GetCallUsageQuery request, CancellationToken cancellationToken)
    {
        var calls = _context.ExternalCallLog.AsNoTracking();

        if (request.Days is > 0)
        {
            var since = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-request.Days.Value);
            calls = calls.Where(c => c.StartedAtUtc >= since);
        }

        // Grouped in memory on the few columns it needs; the bodies are never read
        var rows = await calls
            .Select(c => new
            {
                c.Provider,
                c.Purpose,
                c.PromptVersion,
                c.Succeeded,
                c.PromptTokens,
                c.CompletionTokens,
                c.DurationMs,
                c.StartedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => (r.Provider, r.Purpose, r.PromptVersion))
            .Select(g => new CallUsageDto
            {
                Provider = g.Key.Provider.ToString(),
                Purpose = g.Key.Purpose.ToString(),
                PromptVersion = g.Key.PromptVersion,
                Calls = g.Count(),
                Failed = g.Count(r => !r.Succeeded),
                PromptTokens = g.Sum(r => r.PromptTokens ?? 0),
                CompletionTokens = g.Sum(r => r.CompletionTokens ?? 0),
                TotalDurationMs = g.Sum(r => (long)r.DurationMs),
                FirstAtUtc = g.Min(r => r.StartedAtUtc),
                LastAtUtc = g.Max(r => r.StartedAtUtc)
            })
            .OrderByDescending(u => u.PromptTokens + u.CompletionTokens)
            .ThenByDescending(u => u.Calls)
            .ToList();
    }
}
