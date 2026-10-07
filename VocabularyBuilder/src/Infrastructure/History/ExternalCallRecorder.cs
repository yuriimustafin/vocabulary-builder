using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.History;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.Data;

namespace VocabularyBuilder.Infrastructure.History;

/// <summary>
/// Saves each outbound call on a context of its own, as the user of the scope it was made in.
/// </summary>
/// <remarks>
/// The owner is read from this scope's user and written onto the row before it reaches the
/// other context, which is what keeps it right for background work: the enrichment worker
/// acts as the word's owner on its own scope only, and a fresh scope would know nobody.
///
/// A call made with nobody to act as - there is no such caller today - is dropped rather than
/// filed under no one, because every row in the table belongs to a user.
/// </remarks>
public class ExternalCallRecorder : IExternalCallRecorder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUser _user;
    private readonly ILogger<ExternalCallRecorder> _logger;

    public ExternalCallRecorder(IServiceScopeFactory scopeFactory, IUser user, ILogger<ExternalCallRecorder> logger)
    {
        _scopeFactory = scopeFactory;
        _user = user;
        _logger = logger;
    }

    public async Task RecordAsync(ExternalCallLog call)
    {
        var ownerId = _user.Id;

        if (string.IsNullOrEmpty(ownerId))
        {
            _logger.LogDebug("Not recording a {Provider} call made with no user to file it under", call.Provider);
            return;
        }

        var frame = ExternalCallScope.Current;

        call.OwnerId = ownerId;

        if (call.Purpose == ExternalCallPurpose.Other && frame?.Purpose is { } purpose)
        {
            call.Purpose = purpose;
        }

        call.Target ??= frame?.Target;
        call.WordId ??= frame?.WordId;
        call.ImportId ??= frame?.ImportId;
        call.PromptVersion ??= frame?.PromptVersion;

        if (call.Target is { Length: > 200 })
        {
            call.Target = call.Target[..200];
        }

        if (call.Error is { Length: > 2000 })
        {
            call.Error = call.Error[..2000];
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            context.ExternalCallLog.Add(call);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record a {Provider} call for {Target}", call.Provider, call.Target);
        }
    }
}
