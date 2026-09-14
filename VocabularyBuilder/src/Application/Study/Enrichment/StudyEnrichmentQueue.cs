using System.Threading.Channels;

namespace VocabularyBuilder.Application.Study.Enrichment;

public interface IStudyEnrichmentQueue
{
    /// <summary>
    /// Asks for a word to be filled in. Safe to call repeatedly for the same word: the
    /// enrichment command itself is idempotent, so a duplicate request costs a lookup
    /// rather than a second generation.
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

    private int _pending;

    public int PendingCount => Volatile.Read(ref _pending);

    public void Enqueue(int wordId)
    {
        if (_channel.Writer.TryWrite(wordId))
        {
            Interlocked.Increment(ref _pending);
        }
    }

    public async IAsyncEnumerable<int> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var wordId in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _pending);
            yield return wordId;
        }
    }
}
