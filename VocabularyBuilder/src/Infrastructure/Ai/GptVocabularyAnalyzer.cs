using System.Text.Json;
using System.Text.RegularExpressions;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Infrastructure.Ai;

/// <summary>
/// Asks a model the two questions the import rules cannot answer: what the vocabulary
/// items in a page of notes are, and what headword a term reduces to.
/// </summary>
/// <remarks>
/// Both prompts return JSON arrays rather than one object per term, so a list costs one
/// request instead of one per word. Terms are sent in batches because a long list invites
/// a truncated answer; a batch whose answer cannot be read is reported as unresolved
/// rather than failing the import.
/// </remarks>
public class GptVocabularyAnalyzer : IVocabularyAnalyzer
{
    /// <summary>
    /// Terms per request. Small enough that the answer stays well inside a response, large
    /// enough that a few hundred terms cost a handful of calls.
    /// </summary>
    private const int BatchSize = 40;

    /// <summary>
    /// Identify these prompts without having to match on their prose. The mock client keys
    /// off them, so tests and end-to-end runs never reach a real model.
    /// </summary>
    public const string ExtractionMarker = "NOTES_VOCABULARY_V1";

    public const string LemmaMarker = "LEMMA_LOOKUP_V1";

    private const string ExtractionPrompt =
@"NOTES_VOCABULARY_V1
You are helping a learner turn their lesson notes into vocabulary entries.

The notes contain French vocabulary items, one or more per line. Read them and return
every distinct French item.

Rules:
- A line may pair the item with a translation, separated by =, =>, -, or :. Return only
  the French side; discard the translation.
- A line may hold several separate items divided by commas. Return them separately.
  ""un chien, un chat"" is two items, but ""une pomme de terre"" is one.
- Return the item as written in the notes, including its article. Do not translate,
  correct or expand anything.
- Ignore headings, lesson names, dates, page numbers and any line with no French in it.

Return nothing but a JSON array of strings, for example:
[""un point de vue"", ""migrer"", ""une oeuvre d'art""]

The notes:
";

    private const string LemmaPrompt =
@"LEMMA_LOOKUP_V1
You are a French dictionary assistant. For each term you are given, return the headword
a dictionary would list it under.

Rules:
- A conjugated verb returns its infinitive: ""vous allez"" returns ""aller"".
- A noun with a modifier returns the noun alone, unless the pair is itself a dictionary
  entry: ""les cheveux noirs"" returns ""cheveux"", but ""une pomme de terre"" returns
  ""pomme de terre"" and ""un haricot vert"" returns ""haricot vert"".
- Return the headword bare, with no article and no ""to"" in front of an infinitive.
- A term that is a sentence, a question, a greeting or a fixed conversational expression
  is not a vocabulary item. Return null for its lemma and a short reason instead.

Return nothing but a JSON array, one entry per term, in the order given:
[{""term"": ""vous allez"", ""lemma"": ""aller"", ""reason"": null},
 {""term"": ""il fait beau"", ""lemma"": null, ""reason"": ""Expression""}]

The terms:
";

    private readonly IGptClient _gptClient;

    public GptVocabularyAnalyzer(IGptClient gptClient)
    {
        _gptClient = gptClient;
    }

    public async Task<IReadOnlyList<string>> ExtractItemsAsync(
        string notes,
        Language language,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return Array.Empty<string>();
        }

        var response = await _gptClient.SendMessageAsync(ExtractionPrompt + notes);

        if (string.IsNullOrWhiteSpace(response))
        {
            Console.WriteLine("No response from the model when extracting vocabulary from notes");
            return Array.Empty<string>();
        }

        var items = Deserialize<List<string>>(response);

        if (items == null)
        {
            Console.WriteLine("Could not read the extracted vocabulary from the model's answer");
            return Array.Empty<string>();
        }

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<AnalyzedTerm>> ResolveLemmasAsync(
        IReadOnlyList<string> terms,
        Language language,
        CancellationToken cancellationToken = default)
    {
        if (terms.Count == 0)
        {
            return Array.Empty<AnalyzedTerm>();
        }

        var results = new List<AnalyzedTerm>();

        foreach (var batch in Batch(terms, BatchSize))
        {
            results.AddRange(await ResolveBatchAsync(batch));
        }

        return results;
    }

    private async Task<IReadOnlyList<AnalyzedTerm>> ResolveBatchAsync(IReadOnlyList<string> batch)
    {
        var prompt = LemmaPrompt + JsonSerializer.Serialize(batch);
        var response = await _gptClient.SendMessageAsync(prompt);

        var answers = string.IsNullOrWhiteSpace(response)
            ? null
            : Deserialize<List<LemmaAnswer>>(response);

        if (answers == null)
        {
            Console.WriteLine($"Could not read lemmas for {batch.Count} terms; reporting them as unresolved");
            return batch
                .Select(term => new AnalyzedTerm(term, null, "Could not be analysed"))
                .ToList();
        }

        // Paired by term rather than by position: a model that drops or reorders an entry
        // would otherwise attach every later answer to the wrong word
        var byTerm = new Dictionary<string, LemmaAnswer>(StringComparer.OrdinalIgnoreCase);
        foreach (var answer in answers.Where(a => !string.IsNullOrWhiteSpace(a.Term)))
        {
            byTerm[answer.Term!.Trim()] = answer;
        }

        return batch.Select(term =>
        {
            if (!byTerm.TryGetValue(term.Trim(), out var answer))
            {
                return new AnalyzedTerm(term, null, "Not returned by the analysis");
            }

            var lemma = answer.Lemma?.Trim();

            return string.IsNullOrEmpty(lemma)
                ? new AnalyzedTerm(term, null, string.IsNullOrWhiteSpace(answer.Reason) ? "Not a vocabulary item" : answer.Reason!.Trim())
                : new AnalyzedTerm(term, lemma, null);
        }).ToList();
    }

    private static IEnumerable<IReadOnlyList<T>> Batch<T>(IReadOnlyList<T> items, int size)
    {
        for (var start = 0; start < items.Count; start += size)
        {
            yield return items.Skip(start).Take(size).ToList();
        }
    }

    /// <summary>
    /// Reads the JSON out of an answer that may arrive wrapped in a markdown fence or
    /// trailed by a sentence of commentary.
    /// </summary>
    private static T? Deserialize<T>(string response) where T : class
    {
        var json = ExtractJsonArray(response);

        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error reading the model's answer: {ex.Message}");
            return null;
        }
    }

    private static string ExtractJsonArray(string response)
    {
        var fenced = Regex.Match(response, @"```(?:json)?\s*(\[.*?\])\s*```", RegexOptions.Singleline);

        if (fenced.Success)
        {
            return fenced.Groups[1].Value;
        }

        var bare = Regex.Match(response, @"\[.*\]", RegexOptions.Singleline);

        return bare.Success ? bare.Value : string.Empty;
    }

    private class LemmaAnswer
    {
        public string? Term { get; set; }
        public string? Lemma { get; set; }
        public string? Reason { get; set; }
    }
}
