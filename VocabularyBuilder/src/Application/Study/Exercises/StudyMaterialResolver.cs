using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Exercises;

public interface IStudyMaterialResolver
{
    StudyMaterial Resolve(Word word, WordStudyContent? generated);

    /// <summary>What the word is still missing once existing dictionary data is taken into account.</summary>
    StudyMaterialGaps FindGaps(Word word, WordStudyContent? generated);
}

/// <summary>
/// Composes the material for one word, always preferring what the app already holds.
/// Senses and examples collected from dictionaries are used as they are; generated
/// content only ever fills a hole, which is why a well-sourced word costs nothing to study.
/// </summary>
public class StudyMaterialResolver : IStudyMaterialResolver
{
    public StudyMaterial Resolve(Word word, WordStudyContent? generated)
    {
        return new StudyMaterial
        {
            WordId = word.Id,
            Headword = word.Headword,
            Language = word.Language,
            PartOfSpeech = word.PartOfSpeech,
            Transcription = word.Transcription,
            Article = NounArticleDto.From(word.GetArticle()),
            Meaning = DictionaryMeaning(word) ?? Trimmed(generated?.GeneratedDefinition),
            ContextSentence = DictionaryContextSentence(word) ?? UsableGeneratedSentence(word, generated)
        };
    }

    public StudyMaterialGaps FindGaps(Word word, WordStudyContent? generated)
    {
        var material = Resolve(word, generated);
        var gaps = StudyMaterialGaps.None;

        if (!material.HasMeaning)
        {
            gaps |= StudyMaterialGaps.Meaning;
        }

        if (!material.HasContextSentence)
        {
            gaps |= StudyMaterialGaps.ContextSentence;
        }

        return gaps;
    }

    private static string? DictionaryMeaning(Word word) =>
        word.Senses?
            .Select(s => Trimmed(s.Definition))
            .FirstOrDefault(d => d is not null);

    /// <summary>
    /// An example is only usable if the headword actually appears in it - otherwise there
    /// is nothing for a cloze exercise to blank out.
    /// </summary>
    private static string? DictionaryContextSentence(Word word)
    {
        var fromSenses = word.Senses?
            .Where(s => s.Examples is not null)
            .SelectMany(s => s.Examples!)
            ?? Enumerable.Empty<string>();

        var fromWord = word.Examples ?? Enumerable.Empty<string>();

        return fromSenses.Concat(fromWord)
            .Select(Trimmed)
            .FirstOrDefault(e => e is not null && HeadwordText.Contains(e, word.Headword));
    }

    /// <summary>
    /// Generated sentences are held to the same rule. A model that paraphrased the word
    /// away leaves the gap open rather than producing an unusable cloze.
    /// </summary>
    private static string? UsableGeneratedSentence(Word word, WordStudyContent? generated)
    {
        var sentence = Trimmed(generated?.GeneratedContextSentence);
        return HeadwordText.Contains(sentence, word.Headword) ? sentence : null;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
