using AngleSharp;
using AngleSharp.Html.Parser;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// Mock Oxford parser for testing that uses pre-recorded HTML files
/// </summary>
public class MockOxfordParser : IWordReferenceParser
{
    private readonly string _mockDataPath;
    private readonly OxfordParser _realParser;

    public DictionarySourceType SourceType => DictionarySourceType.Oxford;

    public MockOxfordParser(string? mockDataPath = null)
    {
        _mockDataPath = mockDataPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MockData", "oxford");
        _realParser = new OxfordParser();
        
        if (!Directory.Exists(_mockDataPath))
        {
            Directory.CreateDirectory(_mockDataPath);
        }
    }

    public async Task<IEnumerable<Word>> GetWords(IEnumerable<string> searchedWords)
    {
        var words = new List<Word>();

        foreach (var searchedWord in searchedWords)
        {
            var mockHtml = GetMockHtml(searchedWord);
            if (mockHtml != null)
            {
                var word = await ParseFromHtml(mockHtml);
                if (word != null)
                {
                    words.Add(word);
                }
            }
            else
            {
                // Answer as the dictionary would for a word it does not carry: with nothing.
                // A stub here would be indistinguishable from a real entry to everything
                // downstream, and identical for every word, which quietly ruins anything
                // that compares one word's meaning against another's
                Console.WriteLine($"No mock data found for word: {searchedWord}");
            }
        }

        return words;
    }

    public async Task<IEnumerable<WordParseResult>> GetWordsWithSource(IEnumerable<string> searchedWords)
    {
        var results = new List<WordParseResult>();

        foreach (var searchedWord in searchedWords)
        {
            var mockHtml = GetMockHtml(searchedWord);
            if (mockHtml != null)
            {
                var word = await ParseFromHtml(mockHtml);
                if (word != null)
                {
                    results.Add(new WordParseResult
                    {
                        Word = word,
                        SearchedTerm = searchedWord,
                        SourceHtml = mockHtml,
                        SourceUrl = $"mock://oxford/{GetWordKey(searchedWord)}"
                    });
                }
            }
            else
            {
                // See above: an unrecorded word is a word the dictionary does not have
                Console.WriteLine($"No mock data found for word: {searchedWord}");
            }
        }

        return results;
    }

    public async Task<Word?> GetWordFromCachedHtml(string cachedHtml)
    {
        return await ParseFromHtml(cachedHtml);
    }

    private string? GetMockHtml(string searchedWord)
    {
        var wordKey = GetWordKey(searchedWord);
        var filePath = Path.Combine(_mockDataPath, $"{wordKey}.html");

        if (File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        return null;
    }

    private async Task<Word?> ParseFromHtml(string html)
    {
        // Use the real parser's HTML parsing logic
        return await _realParser.GetWordFromCachedHtml(html);
    }

    private string GetWordKey(string searchedWord)
    {
        // Extract word from URL if it's a URL, otherwise use the word itself
        if (searchedWord.StartsWith("http"))
        {
            var uri = new Uri(searchedWord);
            var segments = uri.Segments;
            return segments.Last().TrimEnd('/').ToLowerInvariant();
        }

        return searchedWord.ToLowerInvariant().Replace(" ", "_");
    }
}
