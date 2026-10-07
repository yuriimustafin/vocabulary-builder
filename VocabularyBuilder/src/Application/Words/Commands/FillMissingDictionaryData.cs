using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Commands;

public class FillMissingDictionaryDataResult
{
    /// <summary>Words that were missing something a dictionary holds.</summary>
    public int Considered { get; set; }

    public int Filled { get; set; }

    /// <summary>Words no dictionary, and no fallback, had an entry for.</summary>
    public int NotFound { get; set; }

    public List<string> FilledWords { get; set; } = new();
}

/// <summary>
/// Fills in every word that is still waiting on a dictionary.
/// </summary>
/// <remarks>
/// A study session fills the words it puts in front of you, which covers a word the first
/// time it comes round. It does not reach a word already answered today, or one scheduled
/// months out - those would go on being shown without an article until their turn came again.
/// This is the sweep for them, and for a vocabulary imported before anything filled words in
/// at all.
///
/// Pass Force to look up words that already have data, which is what backfills something the
/// parser has newly learnt to read.
///
/// One word at a time on purpose. Each is a dictionary request, the parser paces itself
/// between them, and a word the dictionary does not have is counted and stepped over rather
/// than stopping the rest.
/// </remarks>
/// <param name="Force">
/// Look every word up again, including the ones that already have dictionary data, ignoring
/// their cached pages. This is how a field the parser has only just started asking for reaches
/// words collected before it - re-parsing a stored page cannot find a gloss that was never
/// recorded in it.
///
/// It is a dictionary request per word, and where that dictionary is a model it is a bill per
/// word, so pass <paramref name="Limit"/> and work through a collection in batches rather than
/// forcing several hundred in one call.
/// </param>
public record FillMissingDictionaryDataCommand(Language Language, int? Limit = null, bool Force = false)
    : IRequest<FillMissingDictionaryDataResult>;

public class FillMissingDictionaryDataCommandHandler
    : IRequestHandler<FillMissingDictionaryDataCommand, FillMissingDictionaryDataResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public FillMissingDictionaryDataCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<FillMissingDictionaryDataResult> Handle(
        FillMissingDictionaryDataCommand request,
        CancellationToken cancellationToken)
    {
        // Read the words out before deciding: whether one needs filling depends on its
        // senses and its gender together, which is easier to say in memory than in SQL
        var words = await _context.Words
            .Include(w => w.Senses)
            .Where(w => w.Language == request.Language)
            .ToListAsync(cancellationToken);

        var waiting = words
            .Where(w => request.Force || w.IsMissingDictionaryData())
            .OrderBy(w => w.Id)
            .Take(request.Limit ?? int.MaxValue)
            .ToList();

        var result = new FillMissingDictionaryDataResult { Considered = waiting.Count };

        foreach (var word in waiting)
        {
            var outcome = await _sender.Send(
                new FillWordFromDictionaryCommand(word.Id, request.Force), cancellationToken);

            switch (outcome)
            {
                case DictionaryFillOutcome.Filled:
                    result.Filled++;
                    result.FilledWords.Add(word.Headword);
                    break;

                case DictionaryFillOutcome.NotFound:
                    result.NotFound++;
                    break;
            }
        }

        if (result.Considered > 0)
        {
            _context.RecordActivity(
                ActivityAction.DictionaryFillRun,
                language: request.Language,
                summary: $"{(request.Force ? "Forced refill" : "Fill")}: {result.Filled} filled, {result.NotFound} not found, of {result.Considered}",
                details: new { request.Force, request.Limit, result.Considered, result.Filled, result.NotFound });
            await _context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
