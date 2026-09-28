using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public enum DictionaryFillOutcome
{
    /// <summary>The word already had what the dictionary would give it.</summary>
    AlreadyFilled,

    Filled,

    /// <summary>No dictionary, and no fallback, had an entry for it.</summary>
    NotFound,

    WordNotFound
}

/// <summary>
/// Gives one word the things only a dictionary knows: its gender, its part of speech, how it
/// is pronounced, and what it means.
/// </summary>
/// <remarks>
/// A word can enter the vocabulary with nothing but its headword - every import except the
/// bulk one stores it that way on purpose, so that importing several hundred terms costs no
/// more than writing them. Until something fills the rest in, a French noun has no gender,
/// and with no gender <see cref="FrenchArticles"/> declines to guess an article, which is why
/// such a word is shown bare rather than as "une randonnée".
///
/// Export used to be the only thing that filled a word in, which was fine while exporting was
/// the only thing that read one. Studying reads them too, and comes first.
/// </remarks>
/// <param name="Force">
/// Look the word up again even though it already has dictionary data, and ignore its cached
/// page. This is what backfills a field the parser has only just started asking for - a gloss,
/// say: re-parsing the stored page cannot find one that was never recorded in it. It costs a
/// request per word, so nothing does it by default.
/// </param>
public record FillWordFromDictionaryCommand(int WordId, bool Force = false)
    : IRequest<DictionaryFillOutcome>;

public class FillWordFromDictionaryCommandHandler
    : IRequestHandler<FillWordFromDictionaryCommand, DictionaryFillOutcome>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public FillWordFromDictionaryCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<DictionaryFillOutcome> Handle(
        FillWordFromDictionaryCommand request,
        CancellationToken cancellationToken)
    {
        var word = await _context.Words
            .Include(w => w.Senses)
            .FirstOrDefaultAsync(w => w.Id == request.WordId, cancellationToken);

        if (word is null)
        {
            return DictionaryFillOutcome.WordNotFound;
        }

        if (!request.Force && !word.IsMissingDictionaryData())
        {
            return DictionaryFillOutcome.AlreadyFilled;
        }

        var results = await _sender.Send(new LookupWordsFromDictionaryQuery
        {
            Words = new List<string> { word.Headword },
            Language = word.Language,
            SourceType = word.Language.GetDefaultSourceType(),
            IgnoreCache = request.Force
        }, cancellationToken);

        // The lookup is asked about one word, but answers by term rather than by position and
        // returns nothing at all for a word it has no entry for
        var result = results.FirstOrDefault(r =>
            r.SearchedTerm.Equals(word.Headword, StringComparison.OrdinalIgnoreCase) ||
            r.Word.Headword.Equals(word.Headword, StringComparison.OrdinalIgnoreCase));

        if (result is null)
        {
            return DictionaryFillOutcome.NotFound;
        }

        // A forced lookup replaces the senses rather than adding to them. UpsertWord merges -
        // it keeps any sense whose definition it has not seen - and on a re-lookup that is
        // almost every one of them, because the new answer words the same meaning slightly
        // differently. Left merging, a forced sweep leaves a collection fuller of near
        // duplicates each time it is run: "aujourd'hui" came back with "today, on this day"
        // beside a freshly added "today".
        //
        // The cost of replacing is that the current dictionary's answer is the one kept, so
        // forcing a word whose senses came from a richer source swaps them for this one's.
        // That is what asking for a fresh lookup means, and it is why nothing forces by default.
        if (request.Force && word.Senses is { Count: > 0 })
        {
            _context.Senses.RemoveRange(word.Senses);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Upsert rather than assignment: it merges senses, keeps the forms and the cached
        // pages, and leaves alone anything the lookup has no opinion about
        await _sender.Send(new UpsertWordCommand
        {
            Headword = word.Headword,
            Language = word.Language,
            Transcription = result.Word.Transcription,
            PartOfSpeech = result.Word.PartOfSpeech,
            Gender = result.Word.Gender,
            IsPluralOnly = result.Word.IsPluralOnly,
            Frequency = result.Word.Frequency,
            Examples = result.Word.Examples?.ToList(),
            Senses = result.Word.Senses?.ToList(),
            // An encounter is a record of having met the word, and looking it up is not
            // meeting it again
            RecordEncounter = false,
            DictionarySources = result.DictionarySources.Any() ? result.DictionarySources : null,
            Forms = result.Forms.Any() ? result.Forms : null
        }, cancellationToken);

        return DictionaryFillOutcome.Filled;
    }

}
