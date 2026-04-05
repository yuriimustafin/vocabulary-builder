using System.Text.Json;
using VocabularyBuilder.Application.Ai;

namespace VocabularyBuilder.Infrastructure.HttpClients;

/// <summary>
/// Mock GPT client for testing that returns pre-recorded responses
/// </summary>
public class MockGptClient : IGptClient
{
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

    private string GetPromptKey(string prompt)
    {
        // Create a normalized key from the prompt
        return prompt.ToLowerInvariant().Trim();
    }

    private string? ExtractWordFromPrompt(string prompt)
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
