using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VocabularyBuilder.Application.Study.Enrichment;

public interface IStudyEnrichmentQueue
{
    /// <summary>
    /// Asks for a word to be filled in. Safe to call repeatedly for the same word: a word
    /// already waiting is not queued a second time.
    /// </summary>
    void Enqueue(int wordId);

    IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>Queued but not yet processed. Surfaced to the session as a waiting count.</summary>
    int PendingCount { get; }
}

/// <summary>
/// In-process hand-off between the session, which notices a word needs filling in, and the
/// background worker that talks to the model.
///
/// Deliberately not durable: anything lost to a restart is picked up again by the startup
/// sweep over rows still marked pending, so the queue never has to be the record of what
/// is outstanding.
/// </summary>
public class StudyEnrichmentQueue : IStudyEnrichmentQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    /// <summary>
    /// Words waiting, so each is queued once however often it is asked for. Every fetch of
    /// a session asks again for every word on it that is not filled in yet, and with every
    /// word now filled in once, a queue that took each ask would grow by a session's worth
    /// on every fetch - each entry a round of database work for the worker, competing with
    /// the session it is meant to serve.
    /// </summary>
    private readonly ConcurrentDictionary<int, byte> _waiting = new();

    private int _pending;

    public int PendingCount => Volatile.Read(ref _pending);

    public void Enqueue(int wordId)
    {
        if (!_waiting.TryAdd(wordId, 0))
        {
            return;
        }

        if (_channel.Writer.TryWrite(wordId))
        {
            Interlocked.Increment(ref _pending);
        }
        else
        {
            _waiting.TryRemove(wordId, out _);
        }
    }

    public async IAsyncEnumerable<int> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var wordId in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            // Released as it is taken rather than once it is done: a word asked for again
            // while it is being filled in is queued again, and the enrichment command, being
            // idempotent, finds it filled or claimed
            _waiting.TryRemove(wordId, out _);
            Interlocked.Decrement(ref _pending);
            yield return wordId;
        }
    }
}
