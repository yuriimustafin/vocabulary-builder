using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// GPT-based parser for French words, providing English translations and examples
/// </summary>
public class GptFrenchParser : IWordReferenceParser
{
    private readonly IGptClient _gptClient;
    
    private const string SystemPrompt = @"You are a French-English dictionary assistant. For each French word provided, return a JSON response with linguistic information.

Return JSON in this exact format:
{
  ""lemma"": ""base form of the word"",
  ""ipa"": ""IPA pronunciation"",
  ""partOfSpeech"": ""noun/verb/adjective/etc"",
  ""senses"": [
    {
      ""definition"": ""English translation/definition"",
      ""examples"": [
        {
          ""french"": ""French example sentence"",
          ""english"": ""English translation of example""
        }
      ]
    }
  ]
}

Include the most common 1-3 senses. For each sense, provide 1-2 example sentences in French with English translations.";

    public GptFrenchParser(IGptClient gptClient)
    {
        _gptClient = gptClient;
    }

    public async Task<IEnumerable<Word>> GetWords(IEnumerable<string> searchedWords)
    {
        var results = await GetWordsWithSource(searchedWords);
        return results.Select(r => r.Word);
    }

    public async Task<IEnumerable<WordParseResult>> GetWordsWithSource(IEnumerable<string> searchedWords)
    {
        var results = new List<WordParseResult>();

        foreach (var searchedWord in searchedWords)
        {
            try
            {
                var prompt = $"{SystemPrompt}\n\nProvide dictionary information for the French word: \"{searchedWord}\"";
                var response = await _gptClient.SendMessageAsync(prompt);
                
                if (string.IsNullOrEmpty(response))
                {
                    Console.WriteLine($"No response from GPT for word: {searchedWord}");
                    continue;
                }

                var word = ParseGptResponse(response, searchedWord);
                if (word != null)
                {
                    results.Add(new WordParseResult
                    {
                        Word = word,
                        SourceHtml = response, // Store the raw GPT response as "HTML"
                        SourceUrl = $"gpt://french/{searchedWord}"
                    });
                    Console.WriteLine($"Successfully parsed French word: {word.Headword}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing French word '{searchedWord}': {ex.Message}");
            }
        }

        return results;
    }

    public Task<Word?> GetWordFromCachedHtml(string cachedHtml)
    {
        // For GPT, the "cached HTML" is actually the JSON response
        // We can re-parse it without calling GPT again
        try
        {
            var word = ParseGptResponse(cachedHtml, null);
            return Task.FromResult(word);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing cached GPT response: {ex.Message}");
            return Task.FromResult<Word?>(null);
        }
    }

    private Word? ParseGptResponse(string response, string? searchedWord)
    {
        try
        {
            // GPT might wrap the JSON in markdown code blocks or add text, so extract JSON
            var jsonContent = ExtractJsonFromResponse(response);
            
            if (string.IsNullOrEmpty(jsonContent))
            {
                Console.WriteLine("Could not extract JSON from GPT response");
                return null;
            }

            // Parse the GPT response structure
            var gptResponse = JsonSerializer.Deserialize<GptWordResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (gptResponse == null)
            {
                Console.WriteLine("Failed to deserialize GPT response");
                return null;
            }

            // Convert to Word entity
            var word = new Word
            {
                Headword = gptResponse.Lemma ?? searchedWord ?? "unknown",
                Transcription = gptResponse.Ipa,
                PartOfSpeech = gptResponse.PartOfSpeech,
                Language = Language.French,
                Senses = gptResponse.Senses?.Select(s => new Sense
                {
                    Definition = s.Definition,
                    PartOfSpeech = ParsePartOfSpeech(gptResponse.PartOfSpeech),
                    Examples = s.Examples?.Select(e => $"{e.French} ({e.English})").ToList() ?? new List<string>()
                }).ToList() ?? new List<Sense>(),
                Examples = gptResponse.Senses?
                    .SelectMany(s => s.Examples ?? new List<GptExample>())
                    .Select(e => $"{e.French} ({e.English})")
                    .ToList() ?? new List<string>()
            };

            return word;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ParseGptResponse: {ex.Message}");
            return null;
        }
    }

    private PartsOfSpeech ParsePartOfSpeech(string? pos)
    {
        if (string.IsNullOrEmpty(pos))
            return PartsOfSpeech.Unknown;

        var posLower = pos.ToLower().Trim();
        
        return posLower switch
        {
            "noun" or "nom" or "substantif" => PartsOfSpeech.Noun,
            "verb" or "verbe" => PartsOfSpeech.Verb,
            "adjective" or "adjectif" => PartsOfSpeech.Adjective,
            "adverb" or "adverbe" => PartsOfSpeech.Adverb,
            "pronoun" or "pronom" => PartsOfSpeech.Pronoun,
            "preposition" or "préposition" => PartsOfSpeech.Preposition,
            "conjunction" or "conjonction" => PartsOfSpeech.Conjunction,
            "interjection" => PartsOfSpeech.Interjection,
            "article" => PartsOfSpeech.Article,
            _ => PartsOfSpeech.Unknown
        };
    }

    private string ExtractJsonFromResponse(string response)
    {
        // Handle markdown code blocks
        var jsonBlockMatch = System.Text.RegularExpressions.Regex.Match(response, @"```(?:json)?\s*(\{.*?\})\s*```", 
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (jsonBlockMatch.Success)
        {
            return jsonBlockMatch.Groups[1].Value;
        }

        // Look for raw JSON in the response
        var jsonMatch = System.Text.RegularExpressions.Regex.Match(response, @"\{.*\}", 
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (jsonMatch.Success)
        {
            return jsonMatch.Value;
        }

        return string.Empty;
    }

    // DTOs for deserializing GPT response
    private class GptWordResponse
    {
        public string? Lemma { get; set; }
        public string? Ipa { get; set; }
        public string? PartOfSpeech { get; set; }
        public List<GptSense>? Senses { get; set; }
    }

    private class GptSense
    {
        public string Definition { get; set; } = string.Empty;
        public List<GptExample>? Examples { get; set; }
    }

    private class GptExample
    {
        public string French { get; set; } = string.Empty;
        public string English { get; set; } = string.Empty;
    }
}
