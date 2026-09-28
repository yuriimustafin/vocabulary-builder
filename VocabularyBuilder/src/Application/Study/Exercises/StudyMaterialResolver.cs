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
        // Taken as a pair: the gloss says which sense the meaning is, so it is only right
        // beside the meaning it came from. A generated definition has no gloss at all
        var sense = DictionarySense(word);
        var example = DictionaryExample(word);

        return new StudyMaterial
        {
            WordId = word.Id,
            Headword = word.Headword,
            Language = word.Language,
            PartOfSpeech = word.PartOfSpeech,
            Transcription = word.Transcription,
            Article = NounArticleDto.From(word.GetArticle()),
            Meaning = Trimmed(sense?.Definition) ?? Trimmed(generated?.GeneratedDefinition),
            MeaningGloss = sense is null ? null : Trimmed(sense.Gloss),
            ContextSentence = example?.Sentence ?? UsableGeneratedSentence(word, generated),
            ContextSentenceTranslation = example?.Translation
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

    /// <summary>
    /// The first sense that actually says something. Returned whole rather than as a string,
    /// so its gloss travels with the definition it belongs to.
    /// </summary>
    private static Sense? DictionarySense(Word word) =>
        word.Senses?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Definition));

    /// <summary>
    /// An example is only usable if the headword actually appears in it - otherwise there
    /// is nothing for a cloze exercise to blank out.
    /// </summary>
    private static (string Sentence, string? Translation)? DictionaryExample(Word word)
    {
        var fromSenses = word.Senses?
            .Where(s => s.Examples is not null)
            .SelectMany(s => Paired(s.Examples!, s.ExampleTranslations))
            ?? Enumerable.Empty<(string, string?)>();

        var fromWord = word.Examples is null
            ? Enumerable.Empty<(string, string?)>()
            : Paired(word.Examples, word.ExampleTranslations);

        foreach (var (sentence, translation) in fromSenses.Concat(fromWord))
        {
            var trimmed = Trimmed(sentence);

            if (trimmed is not null && HeadwordText.Contains(trimmed, word.Headword))
            {
                return (trimmed, Trimmed(translation));
            }
        }

        return null;
    }

    /// <summary>
    /// Sentences beside their translations, by position. A sentence whose translation is
    /// missing - or that has outlived the list it was stored with - simply comes back alone.
    /// </summary>
    private static IEnumerable<(string Sentence, string? Translation)> Paired(
        IList<string> sentences, IList<string>? translations)
    {
        return sentences.Select((sentence, index) => (
            sentence,
            translations is not null && index < translations.Count ? translations[index] : null));
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
