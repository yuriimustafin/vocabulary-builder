using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Enrichment;

/// <summary>
/// The request sent to the model for a word's study content.
///
/// Only what is missing is asked for: a word with a dictionary definition is never charged
/// for another, and a word whose examples already cover every form it was met in is asked
/// only for what a newer prompt adds.
/// </summary>
/// <remarks>
/// Everything beyond the definition is there to give the word more ways back into memory -
/// the words it is used with, where it comes from, words it is related to, and something
/// it sounds like. Each field is kept short and allowed to be null: a confabulated
/// etymology or a strained mnemonic is worse than none.
/// </remarks>
public static class StudyContentPrompt
{
    /// <summary>
    /// Identifies this prompt without having to match on its prose. The mock client keys
    /// off it, so tests and end-to-end runs never reach a real model.
    /// </summary>
    public const string Marker = "STUDY_CONTENT_V1";

    /// <summary>
    /// Bumped when what the prompt asks for changes. Content from an older version is asked
    /// again, once, for what the new one adds.
    /// </summary>
    public const string Version = "v6";

    /// <summary>Examples asked for when a word has fewer than this many.</summary>
    public const int MinExamples = 2;

    /// <summary>Most examples asked for at once: a few well-chosen sentences, not a page of them.</summary>
    public const int MaxExamples = 3;

    public static string For(
        Word word,
        StudyMaterialGaps gaps,
        IReadOnlyCollection<string>? uncoveredForms = null,
        string? meaning = null,
        IReadOnlyList<string>? sentencesToGloss = null)
    {
        var partOfSpeech = string.IsNullOrWhiteSpace(word.PartOfSpeech) ? "unknown" : word.PartOfSpeech;
        var forms = uncoveredForms ?? Array.Empty<string>();
        var isEnglish = word.Language == Language.English;
        var translationLanguage = isEnglish ? "Ukrainian" : "English";
        var fields = new List<string>();

        if (gaps.HasFlag(StudyMaterialGaps.Meaning))
        {
            fields.Add("""  "definition": a short plain-English definition under 15 words that does not use the word itself""");
        }

        if (gaps.HasFlag(StudyMaterialGaps.Connections))
        {
            fields.Add("""
                  "usage": one line under 20 words naming what the word is typically said of or used with, across its range - for "bright": light, colours, a promising future, a clever person or idea
                """.TrimEnd());
        }

        var wantsExamples = gaps.HasFlag(StudyMaterialGaps.Examples) || gaps.HasFlag(StudyMaterialGaps.ContextSentence);

        if (wantsExamples || forms.Count > 0)
        {
            var count = wantsExamples ? $"{MinExamples} to {MaxExamples}" : forms.Count.ToString();
            var formsLine = forms.Count > 0
                ? $" Include one example for each of these forms, written exactly like this: {string.Join(", ", forms.Select(f => $"\"{f}\""))}."
                : string.Empty;

            // The resolver keeps only sentences that contain the form they name, so the
            // requirement is stated plainly rather than left to be inferred. Named explicitly:
            // a French word with an English definition beside it would otherwise invite an
            // English sentence the word has to be forced into.
            fields.Add($"""
                  "examples": an array of {count} objects {"{"}"sentence", "translation", "form", "collocation", "glosses"{"}"}. Each sentence is short (6 to 14 words), natural, everyday and grammatically complete {word.Language} - articles included - and built around a different word it is commonly used with, so that together they show the range of what it means. "form" is the word exactly as it appears in that sentence. "translation" is the sentence in {translationLanguage}. "collocation" is the {word.Language} phrase the sentence is built around, in its dictionary form - for an English sentence about "bright", "a bright future".{formsLine} "glosses" is every word of the sentence in order, each {"{"}"word", "translation"{"}"}: the word exactly as written there and what it means there in {translationLanguage}.
                """.TrimEnd());
        }

        var toGloss = sentencesToGloss ?? Array.Empty<string>();

        if (toGloss.Count > 0)
        {
            var listed = string.Join("\n", toGloss.Select((sentence, i) => $"  {i + 1}. \"{sentence}\""));

            fields.Add($"""
                  "glosses": an array with one entry for each of these sentences, in this order, each an array of {"{"}"word", "translation"{"}"}: every word of that sentence in order, exactly as written, and what it means there in {translationLanguage}.
                {listed}
                """.TrimEnd());
        }

        if (gaps.HasFlag(StudyMaterialGaps.Connections))
        {
            var relatedIn = isEnglish ? "Ukrainian or French" : "English";
            var soundAlikesIn = isEnglish ? "Ukrainian" : "English or Ukrainian";

            fields.Add($"""
                  "etymology": where the word comes from, under 25 words, as far as it links to a word a learner may know. null if you are not sure - never invent one
                  "cognates": {relatedIn} words sharing its origin or root - including any the etymology names - with how their meaning differs when it does; mark a false friend with "false friend:". null only if there really are none
                  "mnemonic": one or two {soundAlikesIn} words that sound like it when spoken (by pronunciation, not spelling), and one vivid sentence under 25 words linking them to its meaning. null when the word is already obvious from a related word
                """.TrimEnd());
        }

        var meaningLine = string.IsNullOrWhiteSpace(meaning) ? string.Empty : $"\nmeaning: {meaning}";

        return $"""
            {Marker}
            word: "{word.Headword}"
            language: {word.Language}
            part of speech: {partOfSpeech}{meaningLine}

            Return ONLY a JSON object with these keys:
            {string.Join("\n", fields)}

            The quoted examples above only show the shape of an answer. Never copy them into yours - answer for "{word.Headword}" itself, even when it is one of the words the examples use.

            No markdown, no commentary, no code fences. JSON only.
            """;
    }
}
