using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public class ReparseCachedSensesResult
{
    /// <summary>Words that had a cached page to read again.</summary>
    public int Considered { get; set; }

    public int Reparsed { get; set; }

    /// <summary>Words whose cached page could no longer be read.</summary>
    public int Unreadable { get; set; }

    public List<string> ReparsedWords { get; set; } = new();
}

/// <summary>
/// Reads each word's cached dictionary page again and replaces its senses with what the
/// parser makes of it now.
/// </summary>
/// <remarks>
/// For when the parser learns to record something it used to discard, or to keep apart
/// something it used to run together. Senses written by an older parser keep its shape until
/// somebody re-reads the page - and the page is already stored against the word, so this
/// costs no request and can be run whenever the parser changes.
///
/// Only words with a cached page are touched. A word filled in by a model has no page to
/// re-read and is left exactly as it is.
/// </remarks>
public record ReparseCachedSensesCommand(Language Language) : IRequest<ReparseCachedSensesResult>;

public class ReparseCachedSensesCommandHandler
    : IRequestHandler<ReparseCachedSensesCommand, ReparseCachedSensesResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IWordParserFactory _parserFactory;

    public ReparseCachedSensesCommandHandler(
        IApplicationDbContext context,
        IWordParserFactory parserFactory)
    {
        _context = context;
        _parserFactory = parserFactory;
    }

    public async Task<ReparseCachedSensesResult> Handle(
        ReparseCachedSensesCommand request,
        CancellationToken cancellationToken)
    {
        // Every source a word may have been filled from, not only the language's first
        // choice: a word the dictionary did not carry was answered by the model instead, and
        // its answer was cached too. Re-reading one and not the other leaves half the
        // vocabulary in the old shape
        var sourceTypes = ReadableSources(request.Language);

        var cached = await _context.WordDictionarySources
            .Include(source => source.Word)
            .ThenInclude(word => word.Senses)
            .Where(source => sourceTypes.Contains(source.SourceType) && source.Word.Language == request.Language)
            .ToListAsync(cancellationToken);

        // By word, not by page: a word the dictionary did not carry has an answer from the
        // model cached beside it, and re-reading both would replace its senses twice - the
        // second pass trying to delete senses the first had only just attached
        var byWord = cached
            .GroupBy(source => source.WordId)
            .ToList();

        var result = new ReparseCachedSensesResult { Considered = byWord.Count };

        foreach (var group in byWord)
        {
            var word = group.First().Word;
            var parsed = await ReadBestOf(group, request.Language, word.Headword);

            if (parsed is null)
            {
                result.Unreadable++;
                continue;
            }

            Replace(word, parsed);

            result.Reparsed++;
            result.ReparsedWords.Add(word.Headword);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// The sources whose cached page can be read again: the language's dictionary, and the
    /// fallback that answers for words it does not carry. A conjugation page is excluded -
    /// it holds forms rather than senses, and re-reading it here would find none.
    /// </summary>
    private static List<DictionarySourceType> ReadableSources(Language language)
    {
        var sources = new List<DictionarySourceType> { language.GetDefaultSourceType() };

        if (language == Language.French)
        {
            sources.Add(DictionarySourceType.Gpt);
        }

        return sources;
    }

    /// <summary>
    /// Reads the best page a word has, in order of preference: the language's own dictionary
    /// first, the fallback only if that cannot be read. A page that no longer parses is
    /// stepped over rather than counted against the word.
    /// </summary>
    private async Task<Word?> ReadBestOf(
        IEnumerable<WordDictionarySource> sources, Language language, string headword)
    {
        var preferred = sources
            .OrderBy(source => source.SourceType == language.GetDefaultSourceType() ? 0 : 1)
            .ToList();

        foreach (var source in preferred)
        {
            var parser = _parserFactory.GetParser(language, source.SourceType);
            var parsed = await ReadAgain(parser, source.SourceHtml, headword);

            if (parsed?.Senses is { Count: > 0 })
            {
                return parsed;
            }
        }

        return null;
    }

    private static async Task<Word?> ReadAgain(IWordReferenceParser parser, string html, string headword)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        try
        {
            return await parser.GetWordFromCachedHtml(html);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not re-read the cached page for '{headword}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Swaps the senses wholesale rather than merging them.
    /// </summary>
    /// <remarks>
    /// The point is to replace what an older parser wrote, and UpsertWord's merge would keep
    /// it: it adds senses whose definition it has not seen, and every old definition differs
    /// from the new one precisely because the old one has the gloss run into it.
    ///
    /// Everything not re-read - the word's status, its encounters, its tags, how far up the
    /// ladder it has climbed - is left alone. Only what the page says is replaced.
    /// </remarks>
    private void Replace(Word word, Word parsed)
    {
        if (word.Senses is { Count: > 0 })
        {
            _context.Senses.RemoveRange(word.Senses);
        }

        word.Senses = parsed.Senses;
        word.Examples = parsed.Examples;
        word.ExampleTranslations = parsed.ExampleTranslations;

        // Re-read alongside the senses, because an older parser may not have recorded them
        word.Transcription ??= parsed.Transcription;
        word.PartOfSpeech ??= parsed.PartOfSpeech;
        word.Gender ??= parsed.Gender;
    }
}
