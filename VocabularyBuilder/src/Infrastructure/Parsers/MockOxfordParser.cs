using AngleSharp;
using AngleSharp.Html.Parser;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// Mock Oxford parser for testing that uses pre-recorded HTML files
/// </summary>
public class MockOxfordParser : IWordReferenceParser
{
    private readonly string _mockDataPath;
    private readonly OxfordParser _realParser;

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
                Console.WriteLine($"No mock data found for word: {searchedWord}");
                // Return a basic word structure as fallback
                words.Add(CreateDefaultWord(searchedWord));
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
                        SourceHtml = mockHtml,
                        SourceUrl = $"mock://oxford/{GetWordKey(searchedWord)}"
                    });
                }
            }
            else
            {
                Console.WriteLine($"No mock data found for word: {searchedWord}");
                results.Add(new WordParseResult
                {
                    Word = CreateDefaultWord(searchedWord),
                    SourceHtml = "<html><body>Mock data not found</body></html>",
                    SourceUrl = $"mock://oxford/{GetWordKey(searchedWord)}"
                });
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

    private Word CreateDefaultWord(string searchedWord)
    {
        return new Word
        {
            Headword = GetWordKey(searchedWord),
            PartOfSpeech = "noun",
            Transcription = "",
            Senses = new List<Sense>
            {
                new Sense
                {
                    Definition = "Mock definition - data not available",
                    Examples = new List<string>
                    {
                        "This is a mock example."
                    }
                }
            }
        };
    }
}
