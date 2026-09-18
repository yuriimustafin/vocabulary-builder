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
/// One word at a time on purpose. Each is a dictionary request, the parser paces itself
/// between them, and a word the dictionary does not have is counted and stepped over rather
/// than stopping the rest.
/// </remarks>
public record FillMissingDictionaryDataCommand(Language Language, int? Limit = null)
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
            .Where(w => w.IsMissingDictionaryData())
            .OrderBy(w => w.Id)
            .Take(request.Limit ?? int.MaxValue)
            .ToList();

        var result = new FillMissingDictionaryDataResult { Considered = waiting.Count };

        foreach (var word in waiting)
        {
            var outcome = await _sender.Send(new FillWordFromDictionaryCommand(word.Id), cancellationToken);

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

        return result;
    }
}
