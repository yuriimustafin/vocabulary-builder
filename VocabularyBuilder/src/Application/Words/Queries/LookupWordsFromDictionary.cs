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
            var fetchedResults = (await parser.GetWordsWithSource(uncachedWords)).ToList();

            // Anything the dictionary had no entry for gets a second chance
            // against the language's fallback, if it has one
            var missed = uncachedWords
                .Where(word => !fetchedResults.Any(result =>
                    result.SearchedTerm.Equals(word.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missed.Any())
            {
                fetchedResults.AddRange(await FetchFromFallback(missed, request, parser));
            }
            
            foreach (var parseResult in fetchedResults)
            {
                // A fallback result must be cached under the source that
                // actually produced it, not the one that was asked for
                var sourceType = parseResult.SourceUrl.StartsWith("gpt://", StringComparison.OrdinalIgnoreCase)
                    ? DictionarySourceType.Gpt
                    : request.SourceType;

                var dictionarySources = new List<WordDictionarySource>
                {
                    new()
                    {
                        SourceType = sourceType,
                        SourceHtml = parseResult.SourceHtml,
                        SourceUrl = parseResult.SourceUrl
                    }
                };

                // The conjugation page is a second document for the same word,
                // so it is cached under its own source type
                if (!string.IsNullOrEmpty(parseResult.ConjugationHtml))
                {
                    dictionarySources.Add(new WordDictionarySource
                    {
                        SourceType = DictionarySourceType.WordReferenceConjugation,
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
    /// fallback. French falls back from WordReference to GPT, which can answer
    /// for words WordReference does not carry.
    /// </summary>
    private async Task<IEnumerable<WordParseResult>> FetchFromFallback(
        List<string> missedWords,
        LookupWordsFromDictionaryQuery request,
        IWordReferenceParser usedParser)
    {
        var fallbackSourceType = GetFallbackSourceType(request.Language);

        if (fallbackSourceType == null)
        {
            return Array.Empty<WordParseResult>();
        }

        var fallbackParser = _parserFactory.GetParser(request.Language, fallbackSourceType.Value);

        // Nothing to gain from asking the same parser twice
        if (ReferenceEquals(fallbackParser, usedParser) || fallbackParser.SourceType == usedParser.SourceType)
        {
            return Array.Empty<WordParseResult>();
        }

        Console.WriteLine(
            $"Retrying {missedWords.Count} {request.Language} words against {fallbackParser.SourceType}");

        return await fallbackParser.GetWordsWithSource(missedWords);
    }

    private static DictionarySourceType? GetFallbackSourceType(Language language)
    {
        return language switch
        {
            Language.French => DictionarySourceType.Gpt,
            _ => null
        };
    }
}
