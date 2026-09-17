using System.Text.Json;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Enrichment;

public enum EnrichmentOutcome
{
    /// <summary>The word has everything it needs already; nothing was generated.</summary>
    NothingMissing,

    /// <summary>A previous run already filled this word in.</summary>
    AlreadyFilled,

    /// <summary>Another run holds a fresh claim on this word.</summary>
    ClaimedElsewhere,

    Generated,

    /// <summary>Generation failed, or produced nothing usable, and will be retried.</summary>
    Failed,

    /// <summary>Generation has failed too many times; the word is left alone.</summary>
    GivenUp,

    WordNotFound
}

/// <summary>
/// Fills in whatever a word is missing before it can be studied.
/// </summary>
public record EnrichWordStudyContentCommand(int WordId) : IRequest<EnrichmentOutcome>;

/// <summary>
/// Generates only what is actually absent.
///
/// Gaps are computed against the dictionary data the app already holds, so a word with a
/// definition and a usable example never reaches the model at all, and a word missing only
/// a sentence is not asked for a definition it already has.
///
/// Every step is safe to repeat. The unique index on WordId keeps one row per word, a
/// fresh claim makes a concurrent run stand down, and a claim left behind by a crash goes
/// stale and is picked up again.
/// </summary>
public class EnrichWordStudyContentCommandHandler : IRequestHandler<EnrichWordStudyContentCommand, EnrichmentOutcome>
{
    private readonly IApplicationDbContext _context;
    private readonly IGptClient _gptClient;
    private readonly IStudyMaterialResolver _resolver;
    private readonly StudyOptions _options;
    private readonly TimeProvider _timeProvider;

    public EnrichWordStudyContentCommandHandler(
        IApplicationDbContext context,
        IGptClient gptClient,
        IStudyMaterialResolver resolver,
        StudyOptions options,
        TimeProvider timeProvider)
    {
        _context = context;
        _gptClient = gptClient;
        _resolver = resolver;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<EnrichmentOutcome> Handle(EnrichWordStudyContentCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var word = await _context.Words
            .Include(w => w.Senses)
            .FirstOrDefaultAsync(w => w.Id == request.WordId, cancellationToken);

        if (word is null)
        {
            return EnrichmentOutcome.WordNotFound;
        }

        var content = await _context.WordStudyContents
            .FirstOrDefaultAsync(c => c.WordId == request.WordId, cancellationToken);

        var gaps = _resolver.FindGaps(word, content);

        if (gaps == StudyMaterialGaps.None)
        {
            return await NothingLeftToDo(content, cancellationToken);
        }

        if (content is not null && !TryClaim(content, now, out var refusal))
        {
            return refusal;
        }

        if (content is null)
        {
            content = new WordStudyContent
            {
                WordId = word.Id,
                Status = StudyContentStatus.Pending,
                ClaimedAtUtc = now,
                PromptVersion = StudyContentPrompt.Version
            };
            _context.WordStudyContents.Add(content);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another run inserted the row between the read and the write. It holds the
            // claim, so this one steps aside rather than generating the same content twice.
            return EnrichmentOutcome.ClaimedElsewhere;
        }

        return await GenerateAsync(word, content, gaps, now, cancellationToken);
    }

    /// <summary>
    /// A word whose gaps have since been closed - usually because the dictionary data was
    /// filled in elsewhere - is marked done without spending a call.
    /// </summary>
    private async Task<EnrichmentOutcome> NothingLeftToDo(WordStudyContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return EnrichmentOutcome.NothingMissing;
        }

        if (content.Status != StudyContentStatus.Ready)
        {
            content.Status = StudyContentStatus.Ready;
            content.ClaimedAtUtc = null;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return EnrichmentOutcome.AlreadyFilled;
    }

    private bool TryClaim(WordStudyContent content, DateTime now, out EnrichmentOutcome refusal)
    {
        refusal = default;

        if (content.GenerationAttempts >= _options.EnrichmentMaxAttempts)
        {
            // Repeated failures stop costing calls; the word is simply skipped.
            content.Status = StudyContentStatus.Failed;
            refusal = EnrichmentOutcome.GivenUp;
            return false;
        }

        var claimIsFresh = content.Status == StudyContentStatus.Pending
            && content.ClaimedAtUtc is { } claimedAt
            && now - claimedAt < TimeSpan.FromMinutes(_options.EnrichmentStaleClaimMinutes);

        if (claimIsFresh)
        {
            refusal = EnrichmentOutcome.ClaimedElsewhere;
            return false;
        }

        content.Status = StudyContentStatus.Pending;
        content.ClaimedAtUtc = now;
        content.PromptVersion = StudyContentPrompt.Version;
        return true;
    }

    private async Task<EnrichmentOutcome> GenerateAsync(
        Word word, WordStudyContent content, StudyMaterialGaps gaps, DateTime now, CancellationToken cancellationToken)
    {
        GeneratedStudyContent? generated;

        try
        {
            var response = await _gptClient.SendMessageAsync(StudyContentPrompt.For(word, gaps));
            generated = Parse(response);
        }
        catch (Exception ex)
        {
            return await RecordFailure(content, ex.Message, cancellationToken);
        }

        if (generated is null)
        {
            return await RecordFailure(content, "The model returned nothing usable.", cancellationToken);
        }

        if (gaps.HasFlag(StudyMaterialGaps.Meaning) && !string.IsNullOrWhiteSpace(generated.Definition))
        {
            content.GeneratedDefinition = generated.Definition.Trim();
        }

        if (gaps.HasFlag(StudyMaterialGaps.ContextSentence) && !string.IsNullOrWhiteSpace(generated.Sentence))
        {
            content.GeneratedContextSentence = generated.Sentence.Trim();
        }

        // A word needs a meaning to be studied at all. A sentence that came back unusable
        // only costs it the cloze rung, which the ladder already knows how to skip, so that
        // is not worth a retry.
        var remaining = _resolver.FindGaps(word, content);

        if (remaining.HasFlag(StudyMaterialGaps.Meaning))
        {
            return await RecordFailure(content, "No usable definition was produced.", cancellationToken);
        }

        content.Status = StudyContentStatus.Ready;
        content.ClaimedAtUtc = null;
        content.LastError = null;
        await _context.SaveChangesAsync(cancellationToken);

        return EnrichmentOutcome.Generated;
    }

    private async Task<EnrichmentOutcome> RecordFailure(
        WordStudyContent content, string error, CancellationToken cancellationToken)
    {
        content.GenerationAttempts++;
        content.LastError = error;
        content.ClaimedAtUtc = null;

        var givenUp = content.GenerationAttempts >= _options.EnrichmentMaxAttempts;
        content.Status = givenUp ? StudyContentStatus.Failed : StudyContentStatus.Pending;

        await _context.SaveChangesAsync(cancellationToken);

        return givenUp ? EnrichmentOutcome.GivenUp : EnrichmentOutcome.Failed;
    }

    private static GeneratedStudyContent? Parse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var json = ExtractJsonObject(response);

        try
        {
            return json is null
                ? null
                : JsonSerializer.Deserialize<GeneratedStudyContent>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Models wrap JSON in prose or code fences often enough to be worth handling, so the
    /// outermost braces are taken rather than trusting the whole response to be clean.
    /// </summary>
    private static string? ExtractJsonObject(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');

        return start >= 0 && end > start ? response[start..(end + 1)] : null;
    }

    private record GeneratedStudyContent(string? Definition, string? Sentence);
}
