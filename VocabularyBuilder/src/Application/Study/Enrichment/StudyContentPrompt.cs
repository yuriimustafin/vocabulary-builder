using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Enrichment;

/// <summary>
/// The request sent to the model when a word is missing something an exercise needs.
///
/// Only the absent fields are asked for, so a word that already has a definition is never
/// charged for another one.
/// </summary>
public static class StudyContentPrompt
{
    /// <summary>
    /// Identifies this prompt without having to match on its prose. The mock client keys
    /// off it, so tests and end-to-end runs never reach a real model.
    /// </summary>
    public const string Marker = "STUDY_CONTENT_V1";

    /// <summary>Bumped when the wording changes, so content can be regenerated selectively.</summary>
    public const string Version = "v2";

    public static string For(Word word, StudyMaterialGaps gaps)
    {
        var partOfSpeech = string.IsNullOrWhiteSpace(word.PartOfSpeech) ? "unknown" : word.PartOfSpeech;
        var fields = new List<string>();

        if (gaps.HasFlag(StudyMaterialGaps.Meaning))
        {
            fields.Add("""  "definition": a short plain-English definition under 15 words that does not use the word itself""");
        }

        if (gaps.HasFlag(StudyMaterialGaps.ContextSentence))
        {
            // The resolver rejects a sentence that does not contain the headword, so the
            // requirement is stated plainly rather than left to be inferred.
            // Named explicitly: a French word with an English definition beside it would
            // otherwise invite an English sentence the word has to be forced into.
            fields.Add($"""  "sentence": one natural {word.Language} example sentence containing the word exactly as spelled, unchanged""");
        }

        return $"""
            {Marker}
            word: "{word.Headword}"
            language: {word.Language}
            part of speech: {partOfSpeech}

            Return ONLY a JSON object with these keys:
            {string.Join("\n", fields)}

            No markdown, no commentary, no code fences. JSON only.
            """;
    }
}
