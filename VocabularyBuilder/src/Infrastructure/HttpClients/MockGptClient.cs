using System.Text.Json;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Infrastructure.Ai;

using VocabularyBuilder.Infrastructure.Parsers;

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

    /// <summary>
    /// Conjugation recordings, kept in their own folder and so out of the dictionary lookup.
    /// A conjugation prompt quotes the same word as the dictionary prompt for it, so sharing
    /// one folder would let either answer serve the other request.
    /// </summary>
    private readonly Dictionary<string, string> _conjugationResponses;

    private readonly string _mockDataPath;

    public MockGptClient(string? mockDataPath = null)
    {
        _mockDataPath = mockDataPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MockData", "gpt");
        _mockResponses = new Dictionary<string, string>();
        _conjugationResponses = new Dictionary<string, string>();
        LoadMockResponses();
        LoadConjugationResponses();
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

        // Before the recorded-dictionary lookup on purpose: a conjugation prompt names the same
        // word, so the lookup below would answer it with that word's dictionary entry.
        if (prompt.Contains(GptFrenchParser.ConjugationMarker, StringComparison.Ordinal))
        {
            return Task.FromResult(ConjugationResponse(prompt));
        }

        var key = GetPromptKey(prompt);
        var word = ExtractWordFromPrompt(prompt);

        if (_mockResponses.TryGetValue(key, out var response))
        {
            return Task.FromResult<string?>(response);
        }

        // A word named to fail is declined outright, which is the only way to reach the
        // language's fallback source from a test: the model answers for everything otherwise.
        if (word is not null && word.StartsWith(FailureTriggerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Declining the French word: {word}");
            return Task.FromResult<string?>(null);
        }

        // If no exact match, try to find by word.
        //
        // Matched on the quoted form the prompt writes it in, not as a bare substring: a
        // recording is keyed by its whole prompt, and "comprendre" contains "prendre", so a
        // substring match served the wrong word's answer to anything that happened to be a
        // suffix of another recording. It was silent, and it renamed the word - the lemma in
        // the answer is what the import stores.
        if (!string.IsNullOrEmpty(word))
        {
            var quoted = $"\"{word.ToLowerInvariant()}\"";
            var matchingKey = _mockResponses.Keys.FirstOrDefault(k => k.Contains(quoted, StringComparison.Ordinal));
            if (matchingKey != null)
            {
                return Task.FromResult<string?>(_mockResponses[matchingKey]);
            }
        }

        Console.WriteLine($"No mock response found for prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");
        return Task.FromResult<string?>(GetDefaultResponse(word ?? "unknown"));
    }

    /// <summary>
    /// Builds study content for the requested word. Every sentence contains the form it
    /// names verbatim or the handler rejects it - the same rule a real response is held to -
    /// and a form the prompt asks for gets an example of its own.
    /// </summary>
    private static string? StudyContentResponse(string prompt)
    {
        var word = ExtractWordFromPrompt(prompt) ?? "unknown";

        if (word.StartsWith(FailureTriggerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var examples = new List<object>
        {
            Example($"This sentence uses {word} exactly once.", word, $"uses {word}"),
            Example($"People often pair {word} with good company.", word, $"pair {word}"),
            Example($"A second line puts {word} in the middle.", word, $"puts {word}")
        };

        examples.AddRange(RequestedForms(prompt)
            .Select(form => Example($"Here the form {form} appears in a sentence.", form, $"the form {form}")));

        var payload = new
        {
            definition = $"a mock definition of {word}",
            usage = $"said of mock things and ideas, like {word}",
            examples,
            etymology = $"From a mock root of {word}.",
            cognates = $"mock{word} (a related English word)",
            mnemonic = $"{word} sounds like mock; picture a mockingbird saying it."
        };

        return JsonSerializer.Serialize(payload);
    }

    private static object Example(string sentence, string form, string collocation) => new
    {
        sentence,
        translation = $"Translated: {sentence}",
        form,
        collocation
    };

    /// <summary>The forms the prompt asks for an example of, in the quotes it lists them in.</summary>
    private static IEnumerable<string> RequestedForms(string prompt)
    {
        var match = System.Text.RegularExpressions.Regex.Match(prompt, @"written exactly like this: ([^\n]+?)\.(\s|$)");

        if (!match.Success)
        {
            return Enumerable.Empty<string>();
        }

        return System.Text.RegularExpressions.Regex.Matches(match.Groups[1].Value, @"""([^""]+)""")
            .Select(m => m.Groups[1].Value);
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

    /// <summary>
    /// A recorded conjugation, or nothing.
    /// </summary>
    /// <remarks>
    /// Nothing rather than a synthesised table: the moods and tenses of a French verb are not
    /// something to invent plausibly, and a made-up table would let a test assert a conjugation
    /// that no real answer would produce. A verb without a recording simply has no forms, which
    /// is what the parser is written to survive.
    /// </remarks>
    private string? ConjugationResponse(string prompt)
    {
        var verb = ExtractVerbFromPrompt(prompt);

        if (verb is not null && _conjugationResponses.TryGetValue(verb.ToLowerInvariant(), out var recorded))
        {
            return recorded;
        }

        Console.WriteLine($"No recorded conjugation for: {verb ?? "unknown"}");
        return null;
    }

    private static string? ExtractVerbFromPrompt(string prompt)
    {
        var match = System.Text.RegularExpressions.Regex.Match(prompt, @"verb:\s*""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Loads the conjugation recordings, keyed by file name rather than by prompt - there is one
    /// per verb and the prompt is fixed apart from the verb, so the name is the key.
    /// </summary>
    private void LoadConjugationResponses()
    {
        var path = Path.Combine(_mockDataPath, "conjugation");

        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            try
            {
                _conjugationResponses[Path.GetFileNameWithoutExtension(file).ToLowerInvariant()] =
                    File.ReadAllText(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading mock conjugation from {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// The answer for a French word with no recorded response.
    /// </summary>
    /// <remarks>
    /// Has to match the schema <c>GptFrenchParser</c> actually deserialises - lemma, ipa,
    /// gender and nested senses. It once carried an older, flatter shape, which the parser
    /// read as a word with no gender and no senses at all. Nothing failed: the GPT parser was
    /// only the fallback then, and the recorded WordReference pages answered first. The moment
    /// GPT became the default source that silence would have turned into every mocked French
    /// word arriving empty. <c>MockGptFixtureTests</c> now holds the recorded files to this
    /// schema; this one is here beside them.
    ///
    /// A gender is given because the word is declared a noun, and French nouns need one for
    /// an article to be derived. The gloss is null: one sense has nothing to tell apart.
    /// </remarks>
    private string GetDefaultResponse(string word)
    {
        return $$"""
        {
          "lemma": "{{word}}",
          "ipa": null,
          "partOfSpeech": "noun",
          "gender": "masculine",
          "pluralOnly": false,
          "senses": [
            {
              "definition": "a mock definition of {{word}}",
              "gloss": null,
              "examples": [
                {
                  "french": "Une phrase avec {{word}}.",
                  "english": "A sentence with {{word}}."
                }
              ]
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
