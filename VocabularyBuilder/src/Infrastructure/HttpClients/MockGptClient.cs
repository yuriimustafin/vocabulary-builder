using System.Text.Json;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Infrastructure.Ai;

namespace VocabularyBuilder.Infrastructure.HttpClients;

/// <summary>
/// Mock GPT client for testing that returns pre-recorded responses
/// </summary>
public class MockGptClient : IGptClient
{
    /// <summary>
    /// Headword prefix that makes generation fail on purpose, so a study session's failure
    /// handling can be exercised end to end without calling a real model.
    /// </summary>
    private const string FailureTriggerPrefix = "zzfail";

    private readonly Dictionary<string, string> _mockResponses;
    private readonly string _mockDataPath;

    public MockGptClient(string? mockDataPath = null)
    {
        _mockDataPath = mockDataPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MockData", "gpt");
        _mockResponses = new Dictionary<string, string>();
        LoadMockResponses();
    }

    private void LoadMockResponses()
    {
        if (!Directory.Exists(_mockDataPath))
        {
            Directory.CreateDirectory(_mockDataPath);
            return;
        }

        foreach (var file in Directory.GetFiles(_mockDataPath, "*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var content = File.ReadAllText(file);

            try
            {
                var mockData = JsonSerializer.Deserialize<MockGptResponse>(content);
                if (mockData?.Prompt != null && mockData.Response != null)
                {
                    _mockResponses[GetPromptKey(mockData.Prompt)] = mockData.Response;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading mock data from {file}: {ex.Message}");
            }
        }
    }

    public Task<string?> SendMessageAsync(string prompt)
    {
        // Study content has its own shape and has to satisfy the resolver, so it is built
        // rather than served from the recorded responses.
        if (prompt.Contains(StudyContentPrompt.Marker, StringComparison.Ordinal))
        {
            return Task.FromResult(StudyContentResponse(prompt));
        }

        // The import prompts answer in arrays and are asked about a whole list at a time,
        // so they are built here rather than served from the recorded responses.
        if (prompt.Contains(GptVocabularyAnalyzer.ExtractionMarker, StringComparison.Ordinal))
        {
            return Task.FromResult<string?>(ExtractedItemsResponse(prompt));
        }

        if (prompt.Contains(GptVocabularyAnalyzer.LemmaMarker, StringComparison.Ordinal))
        {
            return Task.FromResult<string?>(LemmaResponse(prompt));
        }

        var key = GetPromptKey(prompt);

        if (_mockResponses.TryGetValue(key, out var response))
        {
            return Task.FromResult<string?>(response);
        }

        // If no exact match, try to find by word
        var word = ExtractWordFromPrompt(prompt);
        if (!string.IsNullOrEmpty(word))
        {
            var matchingKey = _mockResponses.Keys.FirstOrDefault(k => k.Contains(word.ToLowerInvariant()));
            if (matchingKey != null)
            {
                return Task.FromResult<string?>(_mockResponses[matchingKey]);
            }
        }

        Console.WriteLine($"No mock response found for prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");
        return Task.FromResult<string?>(GetDefaultResponse(word ?? "unknown"));
    }

    /// <summary>
    /// Builds study content for the requested word. The sentence must contain the headword
    /// verbatim or the resolver rejects it - the same rule a real response is held to.
    /// </summary>
    private static string? StudyContentResponse(string prompt)
    {
        var word = ExtractWordFromPrompt(prompt) ?? "unknown";

        if (word.StartsWith(FailureTriggerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payload = new
        {
            definition = $"a mock definition of {word}",
            sentence = $"This sentence uses {word} exactly once."
        };

        return JsonSerializer.Serialize(payload);
    }

    /// <summary>
    /// Reads the notes the prompt ends with the way the real model is asked to: one item
    /// per line, the translation after "=" or "=>" dropped, and a line of several items
    /// divided at its commas.
    /// </summary>
    private static string ExtractedItemsResponse(string prompt)
    {
        var notes = prompt[(prompt.IndexOf("The notes:", StringComparison.Ordinal) + "The notes:".Length)..];

        var items = notes
            .Split('\n')
            .Select(line => line.Split(new[] { "=>", "=" }, StringSplitOptions.None)[0].Trim())
            .SelectMany(line => line.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(items);
    }

    /// <summary>
    /// Subject pronouns, so a conjugated verb at least loses its pronoun. Kept here rather
    /// than shared with the normalizer: this is a stand-in, and it should not drift into
    /// looking like the real rules.
    /// </summary>
    private static readonly string[] MockPronouns =
    {
        "je", "j'", "tu", "il", "elle", "on", "nous", "vous", "ils", "elles"
    };

    /// <summary>
    /// Drops a leading pronoun and answers with whatever is left. No mock can know a real
    /// infinitive, so this settles for being deterministic, keeping the shape the parser
    /// expects, and leaving compounds ("pomme de terre") intact rather than mangling them.
    /// </summary>
    private static string LemmaResponse(string prompt)
    {
        var start = prompt.LastIndexOf('[');
        var terms = start < 0
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(prompt[start..]) ?? new List<string>();

        var answers = terms.Select(term =>
        {
            var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var lemma = words.Length > 1 && MockPronouns.Contains(words[0], StringComparer.OrdinalIgnoreCase)
                ? string.Join(' ', words.Skip(1))
                : term;

            return new { term, lemma, reason = (string?)null };
        });

        return JsonSerializer.Serialize(answers);
    }

    private string GetPromptKey(string prompt)
    {
        // Create a normalized key from the prompt
        return prompt.ToLowerInvariant().Trim();
    }

    private static string? ExtractWordFromPrompt(string prompt)
    {
        // Try to extract the word being queried from the prompt
        var match = System.Text.RegularExpressions.Regex.Match(prompt, @"word:\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string GetDefaultResponse(string word)
    {
        // Return a default JSON response structure
        return $$"""
        {
          "word": "{{word}}",
          "partOfSpeech": "noun",
          "translation": "translation for {{word}}",
          "definition": "Definition not available in mock data",
          "examples": [
            {
              "french": "Example sentence in French",
              "english": "Example sentence in English"
            }
          ]
        }
        """;
    }
}

public class MockGptResponse
{
    public string? Prompt { get; set; }
    public string? Response { get; set; }
    public DateTime? CreatedAt { get; set; }
}
