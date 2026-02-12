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
    public Word Word { get; set; } = null!;
    public List<WordDictionarySource> DictionarySources { get; set; } = new();
}

public record LookupWordsFromDictionaryQuery : IRequest<List<WordLookupResult>>
{
    public required List<string> Words { get; init; }
    public DictionarySourceType SourceType { get; init; } = DictionarySourceType.Oxford;
}

public class LookupWordsFromDictionaryQueryHandler : IRequestHandler<LookupWordsFromDictionaryQuery, List<WordLookupResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWordReferenceParser _wordReferenceParser;

    public LookupWordsFromDictionaryQueryHandler(
        IApplicationDbContext context,
        IWordReferenceParser wordReferenceParser)
    {
        _context = context;
        _wordReferenceParser = wordReferenceParser;
    }

    public async Task<List<WordLookupResult>> Handle(LookupWordsFromDictionaryQuery request, CancellationToken cancellationToken)
    {
        var results = new List<WordLookupResult>();

        // Check for cached HTML before fetching
        foreach (var wordText in request.Words)
        {
            var normalizedWord = wordText.Trim().ToLower();
            var existingSource = await _context.WordDictionarySources
                .Include(wds => wds.Word)
                .Where(wds => wds.Word.Headword.ToLower() == normalizedWord 
                    && wds.SourceType == request.SourceType)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (existingSource != null)
            {
                // Use cached HTML to parse the word
                Console.WriteLine($"Using cached HTML for: {existingSource.Word.Headword}");
                var parsedWord = await _wordReferenceParser.GetWordFromCachedHtml(existingSource.SourceHtml);
                if (parsedWord != null)
                {
                    results.Add(new WordLookupResult
                    {
                        Word = parsedWord,
                        DictionarySources = new List<WordDictionarySource>()
                    });
                    continue;
                }
            }
            
            // No cache found
            Console.WriteLine($"No cache found for: {wordText}");
        }
        
        // Fetch new words from dictionary (those not in cache)
        var uncachedWords = request.Words.Where(w => 
        {
            var normalizedWord = w.Trim().ToLower();
            return !results.Any(r => r.Word.Headword.ToLower() == normalizedWord);
        }).ToList();
        
        if (uncachedWords.Any())
        {
            Console.WriteLine($"Fetching {uncachedWords.Count} words from dictionary");
            var fetchedResults = await _wordReferenceParser.GetWordsWithSource(uncachedWords);
            
            foreach (var parseResult in fetchedResults)
            {
                var dictionarySource = new WordDictionarySource
                {
                    SourceType = request.SourceType,
                    SourceHtml = parseResult.SourceHtml,
                    SourceUrl = parseResult.SourceUrl
                };
                
                results.Add(new WordLookupResult
                {
                    Word = parseResult.Word,
                    DictionarySources = new List<WordDictionarySource> { dictionarySource }
                });
            }
        }

        return results;
    }
}
