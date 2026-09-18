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
public record FillWordFromDictionaryCommand(int WordId) : IRequest<DictionaryFillOutcome>;

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

        if (!word.IsMissingDictionaryData())
        {
            return DictionaryFillOutcome.AlreadyFilled;
        }

        var results = await _sender.Send(new LookupWordsFromDictionaryQuery
        {
            Words = new List<string> { word.Headword },
            Language = word.Language,
            SourceType = word.Language.GetDefaultSourceType()
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
            Source = WordEncounterSource.Api,

            // Names the fill rather than a meeting with the word: an encounter is a record of
            // having met it, and looking a word up is not meeting it again
            SourceIdentifier = $"dictionary-fill:{word.Id}",
            Context = "Dictionary lookup",
            DictionarySources = result.DictionarySources.Any() ? result.DictionarySources : null,
            Forms = result.Forms.Any() ? result.Forms : null
        }, cancellationToken);

        return DictionaryFillOutcome.Filled;
    }

}
