using System.Diagnostics;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.HttpClients;
using VocabularyBuilder.Infrastructure.Parsers;

namespace VocabularyBuilder.Infrastructure.History;

/// <summary>
/// Records every model call made through the client it wraps.
/// </summary>
/// <remarks>
/// Wraps whichever client is registered - the real one, the recorded one, a test's own - so no
/// call can go unrecorded by being made through the wrong implementation. A client that can
/// say more than the answer (<see cref="IDetailedGptClient"/>) gets its model, status and
/// token counts kept too.
/// </remarks>
public class RecordingGptClient : IGptClient
{
    private readonly IExternalCallRecorder _recorder;

    public RecordingGptClient(IGptClient inner, IExternalCallRecorder recorder)
    {
        Inner = inner;
        _recorder = recorder;
    }

    /// <summary>The client actually answering, for anything that needs to know which.</summary>
    public IGptClient Inner { get; }

    public async Task<string?> SendMessageAsync(string message)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        GptCompletion completion;

        try
        {
            completion = Inner is IDetailedGptClient detailed
                ? await detailed.CompleteAsync(message)
                : new GptCompletion(await Inner.SendMessageAsync(message));
        }
        catch (Exception ex)
        {
            await Record(message, new GptCompletion(null, Error: ex.Message), startedAt, stopwatch);
            throw;
        }

        await Record(message, completion, startedAt, stopwatch);

        return completion.Content;
    }

    private Task Record(string prompt, GptCompletion completion, DateTime startedAt, Stopwatch stopwatch) =>
        _recorder.RecordAsync(new ExternalCallLog
        {
            StartedAtUtc = startedAt,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            Provider = ExternalCallProvider.Gpt,
            Model = completion.Model,
            Request = prompt,
            Response = completion.Content,
            ResponseLength = completion.Content?.Length,
            StatusCode = completion.StatusCode,
            Succeeded = !string.IsNullOrEmpty(completion.Content) && completion.Error is null,
            Error = completion.Error ?? (string.IsNullOrEmpty(completion.Content) ? "Empty response" : null),
            PromptTokens = completion.PromptTokens,
            CompletionTokens = completion.CompletionTokens,
            IsMock = completion.IsMock
        });
}

/// <summary>
/// Records every WordReference page fetched through the loader it wraps - URL, status, size
/// and time, not the page, which is cached against the word already.
/// </summary>
public class RecordingWordReferencePageLoader : IWordReferencePageLoader
{
    private readonly IExternalCallRecorder _recorder;

    public RecordingWordReferencePageLoader(IWordReferencePageLoader inner, IExternalCallRecorder recorder)
    {
        Inner = inner;
        _recorder = recorder;
    }

    public IWordReferencePageLoader Inner { get; }

    public async Task<string?> GetPageAsync(string url)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        PageFetch fetch;

        try
        {
            fetch = Inner is IDetailedPageLoader detailed
                ? await detailed.FetchAsync(url)
                : new PageFetch(await Inner.GetPageAsync(url));
        }
        catch (Exception ex)
        {
            await Record(url, new PageFetch(null, Error: ex.Message), startedAt, stopwatch);
            throw;
        }

        await Record(url, fetch, startedAt, stopwatch);

        return fetch.Html;
    }

    private Task Record(string url, PageFetch fetch, DateTime startedAt, Stopwatch stopwatch) =>
        _recorder.RecordAsync(new ExternalCallLog
        {
            StartedAtUtc = startedAt,
            DurationMs = (int)stopwatch.ElapsedMilliseconds,
            Provider = ExternalCallProvider.WordReference,
            Purpose = url.Contains("frverbs.aspx", StringComparison.OrdinalIgnoreCase)
                ? ExternalCallPurpose.Conjugation
                : ExternalCallPurpose.DictionaryEntry,
            Url = url,
            ResponseLength = fetch.Html?.Length,
            StatusCode = fetch.StatusCode,
            Succeeded = fetch.Html is not null,
            Error = fetch.Error ?? (fetch.Html is null ? "No page" : null),
            IsMock = fetch.IsMock
        });
}
