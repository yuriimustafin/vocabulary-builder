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
    public const string Version = "v3";

    /// <summary>Examples asked for when a word has fewer than this many.</summary>
    public const int MinExamples = 3;

    public static string For(
        Word word,
        StudyMaterialGaps gaps,
        IReadOnlyCollection<string>? uncoveredForms = null,
        string? meaning = null)
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
            var count = wantsExamples ? $"{MinExamples} to 5" : forms.Count.ToString();
            var formsLine = forms.Count > 0
                ? $" Include one example for each of these forms, written exactly like this: {string.Join(", ", forms.Select(f => $"\"{f}\""))}."
                : string.Empty;

            // The resolver keeps only sentences that contain the form they name, so the
            // requirement is stated plainly rather than left to be inferred. Named explicitly:
            // a French word with an English definition beside it would otherwise invite an
            // English sentence the word has to be forced into.
            fields.Add($"""
                  "examples": an array of {count} objects {"{"}"sentence", "translation", "form", "collocation"{"}"}. Each sentence is short (6 to 14 words), natural, everyday {word.Language}, and built around a different word it is commonly used with, so that together they show the range of what it means. "form" is the word exactly as it appears in that sentence. "translation" is the sentence in {translationLanguage}. "collocation" is the phrase the sentence is built around, such as "a bright future".{formsLine}
                """.TrimEnd());
        }

        if (gaps.HasFlag(StudyMaterialGaps.Connections))
        {
            var relatedIn = isEnglish ? "Ukrainian or French" : "English";
            var soundAlikesIn = isEnglish ? "Ukrainian" : "English or Ukrainian";

            fields.Add($"""
                  "etymology": where the word comes from, under 25 words, as far as it links to a word a learner may know. null if you are not sure - never invent one
                  "cognates": {relatedIn} words sharing its origin, with how their meaning differs when it does; mark a false friend with "false friend:". null if there are none
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

            No markdown, no commentary, no code fences. JSON only.
            """;
    }
}
