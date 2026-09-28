using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Queries;

/// <summary>
/// Result of looking up words from dictionary, including cached and fetched words
/// </summary>
public class WordLookupResult
{
    public string SearchedTerm { get; set; } = string.Empty;
    public Word Word { get; set; } = null!;
    public List<WordDictionarySource> DictionarySources { get; set; } = new();

    /// <summary>
    /// Inflected forms discovered alongside the entry, e.g. a verb's conjugation
    /// </summary>
    public List<WordForm> Forms { get; set; } = new();
}

public record LookupWordsFromDictionaryQuery : IRequest<List<WordLookupResult>>
{
    public required List<string> Words { get; init; }
    public Language Language { get; init; } = Language.English;
    public DictionarySourceType SourceType { get; init; } = DictionarySourceType.Oxford;

    /// <summary>
    /// Ask the dictionary again even for a word whose page is already cached.
    /// </summary>
    /// <remarks>
    /// Re-parsing the cached page only ever recovers what that page already says, so it cannot
    /// pick up a field the parser has newly learnt to ask for - a gloss recorded before the
    /// prompt requested one is not in the stored response to find. That needs a fresh answer,
    /// which costs a request, which is why it is off unless asked for.
    /// </remarks>
    public bool IgnoreCache { get; init; }
}

public class LookupWordsFromDictionaryQueryHandler : IRequestHandler<LookupWordsFromDictionaryQuery, List<WordLookupResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWordParserFactory _parserFactory;

    public LookupWordsFromDictionaryQueryHandler(
        IApplicationDbContext context,
        IWordParserFactory parserFactory)
    {
        _context = context;
        _parserFactory = parserFactory;
    }

    public async Task<List<WordLookupResult>> Handle(LookupWordsFromDictionaryQuery request, CancellationToken cancellationToken)
    {
        // Get the appropriate parser based on language and source type
        var parser = _parserFactory.GetParser(request.Language, request.SourceType);
        var results = new List<WordLookupResult>();

        // Check for cached HTML before fetching
        foreach (var wordText in request.Words)
        {
            if (request.IgnoreCache)
            {
                break;
            }

            var normalizedWord = wordText.Trim().ToLower();
            var existingSource = await _context.WordDictionarySources
                .Include(wds => wds.Word)
                .Where(wds => wds.Word.Headword.ToLower() == normalizedWord 
                    && wds.SourceType == request.SourceType
                    && wds.Word.Language == request.Language)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (existingSource != null)
            {
                // Use cached HTML to parse the word
                Console.WriteLine($"Using cached {request.Language} word for: {existingSource.Word.Headword}");
                var parsedWord = await parser.GetWordFromCachedHtml(existingSource.SourceHtml);
                if (parsedWord != null)
                {
                    results.Add(new WordLookupResult
                    {
                        SearchedTerm = normalizedWord,
                        Word = parsedWord,
                        DictionarySources = new List<WordDictionarySource>()
                    });
                    continue;
                }
            }
            
            // No cache found
            Console.WriteLine($"No cache found for {request.Language} word: {wordText}");
        }
        
        // Fetch new words from dictionary (those not in cache)
        var uncachedWords = request.Words.Where(w => 
        {
            var normalizedWord = w.Trim().ToLower();
            return !results.Any(r => r.Word.Headword.ToLower() == normalizedWord);
        }).ToList();
        
        if (uncachedWords.Any())
        {
            Console.WriteLine($"Fetching {uncachedWords.Count} {request.Language} words from {request.SourceType} dictionary");
            // Tagged with the parser that produced each one, because the fallback's results
            // are mixed in below and have to be cached under their own source
            var fetchedResults = (await parser.GetWordsWithSource(uncachedWords))
                .Select(result => (Result: result, Source: parser.SourceType))
                .ToList();

            // Anything the dictionary had no entry for gets a second chance
            // against the language's fallback, if it has one
            var missed = uncachedWords
                .Where(word => !fetchedResults.Any(fetched =>
                    fetched.Result.SearchedTerm.Equals(word.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missed.Any())
            {
                fetchedResults.AddRange(await FetchFromFallback(missed, request, parser));
            }
            
            foreach (var (parseResult, sourceType) in fetchedResults)
            {
                // A fallback result is cached under the source that actually produced it,
                // not the one that was asked for. Read from the parser rather than sniffed
                // out of the URL: a "gpt://" prefix only identified the one source that
                // happened to be the fallback, so the day the pair was inverted a
                // WordReference result would have been filed as a GPT one.
                var dictionarySources = new List<WordDictionarySource>
                {
                    new()
                    {
                        SourceType = sourceType,
                        SourceHtml = parseResult.SourceHtml,
                        SourceUrl = parseResult.SourceUrl
                    }
                };

                // The conjugation table is a second document for the same word, so it is
                // cached under its own source type - and under the one belonging to whichever
                // source produced it, so a model-written table is not filed as WordReference's
                if (!string.IsNullOrEmpty(parseResult.ConjugationHtml))
                {
                    dictionarySources.Add(new WordDictionarySource
                    {
                        SourceType = ConjugationSourceFor(sourceType),
                        SourceHtml = parseResult.ConjugationHtml,
                        SourceUrl = parseResult.ConjugationUrl
                    });
                }
                
                results.Add(new WordLookupResult
                {
                    SearchedTerm = parseResult.SearchedTerm.Trim().ToLower(),
                    Word = parseResult.Word,
                    DictionarySources = dictionarySources,
                    Forms = parseResult.Forms.ToList()
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Retry the words a dictionary had no entry for against the language's
    /// fallback. French falls back from GPT to WordReference.
    /// </summary>
    /// <remarks>
    /// That pair used to be the other way round. WordReference blocks the deployed host with
    /// a 418, so it answered for nothing in production; GPT leads now and WordReference is
    /// kept as the second try rather than removed, for the environments where it does work.
    /// In practice the model answers for almost everything, so this rarely runs.
    /// </remarks>
    private async Task<IEnumerable<(WordParseResult Result, DictionarySourceType Source)>> FetchFromFallback(
        List<string> missedWords,
        LookupWordsFromDictionaryQuery request,
        IWordReferenceParser usedParser)
    {
        var fallbackSourceType = GetFallbackSourceType(request.Language);

        if (fallbackSourceType == null)
        {
            return Array.Empty<(WordParseResult, DictionarySourceType)>();
        }

        var fallbackParser = _parserFactory.GetParser(request.Language, fallbackSourceType.Value);

        // Nothing to gain from asking the same parser twice
        if (ReferenceEquals(fallbackParser, usedParser) || fallbackParser.SourceType == usedParser.SourceType)
        {
            return Array.Empty<(WordParseResult, DictionarySourceType)>();
        }

        Console.WriteLine(
            $"Retrying {missedWords.Count} {request.Language} words against {fallbackParser.SourceType}");

        var results = await fallbackParser.GetWordsWithSource(missedWords);

        return results.Select(result => (result, fallbackParser.SourceType));
    }

    /// <summary>
    /// Where a conjugation table produced by <paramref name="sourceType"/> is cached.
    /// </summary>
    /// <remarks>
    /// A word caches one document per source type, so a table cannot share the entry's own.
    /// Anything without a conjugation type of its own falls back to WordReference's, which is
    /// where the only other table comes from.
    /// </remarks>
    public static DictionarySourceType ConjugationSourceFor(DictionarySourceType sourceType) =>
        sourceType switch
        {
            DictionarySourceType.Gpt => DictionarySourceType.GptConjugation,
            _ => DictionarySourceType.WordReferenceConjugation
        };

    private static DictionarySourceType? GetFallbackSourceType(Language language)
    {
        return language switch
        {
            Language.French => DictionarySourceType.WordReference,
            _ => null
        };
    }
}
