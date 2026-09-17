using MediatR;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.Data;

namespace VocabularyBuilder.Web.Services;

/// <summary>
/// Fills words in away from the request that noticed they were incomplete, so a session
/// can start on the words that are already usable instead of waiting on a model call.
///
/// On start it sweeps up anything left pending by a previous run. Claims are only trusted
/// for a few minutes, so a word abandoned mid-generation by a crash is picked up again
/// rather than being stranded as permanently in progress.
/// </summary>
public class StudyContentEnrichmentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStudyEnrichmentQueue _queue;
    private readonly StudyOptions _options;
    private readonly ILogger<StudyContentEnrichmentWorker> _logger;

    public StudyContentEnrichmentWorker(
        IServiceScopeFactory scopeFactory,
        IStudyEnrichmentQueue queue,
        StudyOptions options,
        ILogger<StudyContentEnrichmentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueAbandonedAsync(stoppingToken);

        await foreach (var wordId in _queue.ReadAllAsync(stoppingToken))
        {
            await EnrichAsync(wordId, stoppingToken);
        }
    }

    private async Task EnrichAsync(int wordId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var outcome = await sender.Send(new EnrichWordStudyContentCommand(wordId), cancellationToken);

            _logger.LogInformation("Study content for word {WordId}: {Outcome}", wordId, outcome);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One bad word must not take the worker down and stall every other word behind it.
            _logger.LogError(ex, "Failed to enrich study content for word {WordId}", wordId);
        }
    }

    /// <summary>
    /// Anything still marked pending with a stale claim was interrupted rather than
    /// finished, so it goes back on the queue.
    /// </summary>
    private async Task RequeueAbandonedAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var staleBefore = timeProvider.GetUtcNow().UtcDateTime
                .AddMinutes(-_options.EnrichmentStaleClaimMinutes);

            var abandoned = await context.WordStudyContents
                .AsNoTracking()
                .Where(c => c.Status == StudyContentStatus.Pending)
                .Where(c => c.ClaimedAtUtc == null || c.ClaimedAtUtc < staleBefore)
                .Where(c => c.GenerationAttempts < _options.EnrichmentMaxAttempts)
                .Select(c => c.WordId)
                .ToListAsync(cancellationToken);

            foreach (var wordId in abandoned)
            {
                _queue.Enqueue(wordId);
            }

            if (abandoned.Count > 0)
            {
                _logger.LogInformation("Requeued {Count} words left unfinished by a previous run", abandoned.Count);
            }
        }
        catch (Exception ex)
        {
            // A failed sweep costs a retry later; it must not stop the worker starting.
            _logger.LogError(ex, "Could not sweep unfinished study content");
        }
    }
}
