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

    public DictionarySourceType SourceType => DictionarySourceType.Gpt;
    
    private const string SystemPrompt = @"You are a French-English dictionary assistant. For each French word provided, return a JSON response with linguistic information.

Return JSON in this exact format:
{
  ""lemma"": ""base form of the word"",
  ""ipa"": ""IPA pronunciation"",
  ""partOfSpeech"": ""noun/verb/adjective/etc"",
  ""gender"": ""masculine, feminine or both - for nouns only, otherwise null"",
  ""pluralOnly"": ""true only for nouns used only in the plural, such as gens or vacances"",
  ""senses"": [
    {
      ""definition"": ""English translation/definition"",
      ""gloss"": ""which sense this is, said in French - a synonym or short phrase, no parentheses, null when there is nothing to distinguish"",
      ""gender"": ""this sense's gender when it differs from the word's, otherwise null"",
      ""examples"": [
        {
          ""french"": ""French example sentence"",
          ""english"": ""English translation of example""
        }
      ]
    }
  ]
}

Include the most common 1-3 senses. For each sense, provide 1-2 example sentences in French with English translations.

Mark an aspirated h by starting the IPA with U+02C8, the way a dictionary does: ""hache"" is
""ˈaʃ"" and takes ""la"", where ""homme"" is ""ɔm"" and takes ""l'"". The article is derived from
gender and pronunciation, so a missing mark spells the word with the wrong article.

A few nouns take a different article in different senses - ""livre"" is ""le livre"" for a book
and ""la livre"" for a pound. Give those senses their own gender; leave it null on every sense of
a word whose gender does not change, which is almost all of them.

The gloss names a sense rather than giving it, the way a French-English dictionary prints a
short French indication before the translations: for ""prendre"" it is ""saisir"" rather than
""to take"". Keep it in French, keep it shorter than the definition, and never restate the
English translation. Return null for it when a word has one sense, or when the senses are
already told apart by their translations - a gloss that adds nothing is worse than none.";

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
                        SearchedTerm = searchedWord,
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
            var partOfSpeech = ParsePartOfSpeech(gptResponse.PartOfSpeech);

            // Gender only means something for a noun; a model asked about a verb may
            // still volunteer one
            var gender = partOfSpeech == PartsOfSpeech.Noun ? ParseGender(gptResponse.Gender) : null;
            var isPluralOnly = gender is not null && IsTrue(gptResponse.PluralOnly);

            var word = new Word
            {
                Headword = gptResponse.Lemma ?? searchedWord ?? "unknown",
                Transcription = gptResponse.Ipa,
                PartOfSpeech = gptResponse.PartOfSpeech,
                Gender = gender,
                IsPluralOnly = isPluralOnly,
                Language = Language.French,
                Senses = gptResponse.Senses?.Select(s => new Sense
                {
                    Definition = s.Definition,
                    // Normalised the way WordReferenceFrenchParser normalises the gloss it
                    // reads out of the middle cell, so both sources store the same shape
                    Gloss = NormalizeGloss(s.Gloss),
                    PartOfSpeech = partOfSpeech,
                    // A sense may carry its own gender, which is how "le livre" (a book) and
                    // "la livre" (a pound) end up on one headword. Only nouns, and only when
                    // the model actually said so - otherwise the word's own gender stands.
                    Gender = partOfSpeech == PartsOfSpeech.Noun
                        ? ParseGender(s.Gender) ?? gender
                        : null,
                    IsPluralOnly = isPluralOnly,
                    // Kept apart, as the dictionary parser keeps them: a sentence written
                    // with its own translation trailing after it ends up inside the gap of a
                    // cloze exercise
                    Examples = s.Examples?.Select(e => e.French ?? string.Empty).ToList() ?? new List<string>(),
                    ExampleTranslations = s.Examples?.Select(e => e.English ?? string.Empty).ToList()
                }).ToList() ?? new List<Sense>(),
                Examples = gptResponse.Senses?
                    .SelectMany(s => s.Examples ?? new List<GptExample>())
                    .Select(e => e.French ?? string.Empty)
                    .ToList() ?? new List<string>(),
                ExampleTranslations = gptResponse.Senses?
                    .SelectMany(s => s.Examples ?? new List<GptExample>())
                    .Select(e => e.English ?? string.Empty)
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
    /// <summary>
    /// A gloss is optional, so anything empty becomes null rather than an empty line on a
    /// card. Parentheses are stripped because a model asked for "saisir" will sometimes
    /// answer "(saisir)", and the string "null" because it sometimes answers that too.
    /// </summary>
    private static string? NormalizeGloss(string? gloss)
    {
        if (string.IsNullOrWhiteSpace(gloss))
        {
            return null;
        }

        var trimmed = gloss.Trim().Trim('(', ')', ' ');

        return trimmed.Length == 0 || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }

    private static GrammaticalGender? ParseGender(string? gender)
    {
        return gender?.Trim().ToLowerInvariant() switch
        {
            "masculine" or "masculin" or "m" => GrammaticalGender.Masculine,
            "feminine" or "féminin" or "f" => GrammaticalGender.Feminine,
            "both" or "common" or "mf" => GrammaticalGender.Common,
            _ => null
        };
    }

    /// <summary>Models return a JSON boolean or the string "true" about equally often.</summary>
    private static bool IsTrue(JsonElement? value)
    {
        return value?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => bool.TryParse(value.Value.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    private class GptWordResponse
    {
        public string? Lemma { get; set; }
        public string? Ipa { get; set; }
        public string? PartOfSpeech { get; set; }
        public string? Gender { get; set; }
        public JsonElement? PluralOnly { get; set; }
        public List<GptSense>? Senses { get; set; }
    }

    private class GptSense
    {
        public string Definition { get; set; } = string.Empty;
        public string? Gloss { get; set; }

        /// <summary>Set only where a sense's gender differs from the word's, as in "livre".</summary>
        public string? Gender { get; set; }

        public List<GptExample>? Examples { get; set; }
    }

    private class GptExample
    {
        public string French { get; set; } = string.Empty;
        public string English { get; set; } = string.Empty;
    }
}
