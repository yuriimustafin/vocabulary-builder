using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Application.Study.Exercises.Definitions;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// A word's stored example sentences, and the forms it has been met in.
/// </summary>
/// <param name="Examples">The word's <see cref="StudyExample"/> rows.</param>
/// <param name="EncounterForms">Forms the word was met in, lower-cased.</param>
/// <param name="PreferredId">
/// The example an exercise was built on, when resolving for an answer to it - so the
/// feedback shows the sentence that was actually asked.
/// </param>
public record StudyExampleSet(
    IReadOnlyList<StudyExample> Examples,
    IReadOnlyCollection<string> EncounterForms,
    int? PreferredId = null)
{
    public static readonly StudyExampleSet None = new(Array.Empty<StudyExample>(), Array.Empty<string>());
}

public interface IStudyMaterialResolver
{
    StudyMaterial Resolve(Word word, WordStudyContent? generated, StudyExampleSet? examples = null);

    /// <summary>What the word is still missing once existing dictionary data is taken into account.</summary>
    StudyMaterialGaps FindGaps(Word word, WordStudyContent? generated, StudyExampleSet? examples = null);

    /// <summary>Forms the word was met in that no example sentence uses yet.</summary>
    IReadOnlyList<string> UncoveredForms(Word word, StudyExampleSet examples);

    /// <summary>Stored examples with a translation but no word-by-word glosses yet.</summary>
    IReadOnlyList<StudyExample> UnglossedExamples(StudyExampleSet examples);
}

/// <summary>
/// Composes the material for one word, always preferring what the app already holds.
/// Senses and examples collected from dictionaries are used as they are; generated
/// content only ever fills a hole, which is why a well-sourced word costs nothing to study.
/// </summary>
public class StudyMaterialResolver : IStudyMaterialResolver
{
    public StudyMaterial Resolve(Word word, WordStudyContent? generated, StudyExampleSet? examples = null)
    {
        // Taken as a pair: the gloss says which sense the meaning is, so it is only right
        // beside the meaning it came from. A generated definition has no gloss at all
        var sense = DictionarySense(word);
        var stored = ChooseExample(examples ?? StudyExampleSet.None);
        var dictionary = stored is null ? DictionaryExample(word) : null;
        var generatedSentence = stored is null && dictionary is null ? UsableGeneratedSentence(word, generated) : null;

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
            ContextSentence = stored?.Sentence ?? dictionary?.Sentence ?? generatedSentence,
            ContextForm = stored?.Form ?? (dictionary is not null || generatedSentence is not null ? word.Headword : null),
            ContextSentenceTranslation = stored is not null ? Trimmed(stored.Translation) : dictionary?.Translation,
            ExampleId = stored?.Id,
            Usage = Trimmed(generated?.Usage),
            Etymology = Trimmed(generated?.Etymology),
            Cognates = Trimmed(generated?.Cognates),
            Mnemonic = Trimmed(generated?.Mnemonic),
            ContextCollocation = Trimmed(stored?.Collocation),
            ContextGlosses = Glosses(stored)
        };
    }

    public StudyMaterialGaps FindGaps(Word word, WordStudyContent? generated, StudyExampleSet? examples = null)
    {
        var set = examples ?? StudyExampleSet.None;
        var material = Resolve(word, generated, set);
        var gaps = StudyMaterialGaps.None;

        if (!material.HasMeaning)
        {
            gaps |= StudyMaterialGaps.Meaning;
        }

        if (!material.HasContextSentence)
        {
            gaps |= StudyMaterialGaps.ContextSentence;
        }

        if (set.Examples.Count(Usable) < StudyContentPrompt.MinExamples)
        {
            gaps |= StudyMaterialGaps.Examples;
        }

        if (UncoveredForms(word, set).Count > 0)
        {
            gaps |= StudyMaterialGaps.Forms;
        }

        // The version is set only when a generation succeeds, so content from before these
        // fields existed - or a word never filled in at all - is asked for them once. Glosses
        // for sentences stored before they were asked for go with that same once: a sentence
        // the model leaves unglossed is not worth a call on every later run.
        if (generated is null || generated.PromptVersion != StudyContentPrompt.Version)
        {
            gaps |= StudyMaterialGaps.Connections;

            if (UnglossedExamples(set).Count > 0)
            {
                gaps |= StudyMaterialGaps.Glosses;
            }
        }

        return gaps;
    }

    public IReadOnlyList<StudyExample> UnglossedExamples(StudyExampleSet examples) =>
        examples.Examples
            .Where(e => Usable(e) && !string.IsNullOrWhiteSpace(e.Translation) && (e.GlossWords is null || e.GlossWords.Count == 0))
            .OrderBy(e => e.Id)
            .ToList();

    /// <summary>
    /// What each word of a stored example means there, keyed the way a sentence's pieces are
    /// compared - lower case, no punctuation round it. Empty for a sentence never glossed.
    /// </summary>
    private static Dictionary<string, string> Glosses(StudyExample? example)
    {
        var glosses = new Dictionary<string, string>();
        var words = example?.GlossWords;
        var meanings = example?.GlossTranslations;

        for (var i = 0; i < (words?.Count ?? 0) && i < (meanings?.Count ?? 0); i++)
        {
            if (!string.IsNullOrWhiteSpace(meanings![i]))
            {
                glosses.TryAdd(TranslationToSentenceScrambleExerciseDefinition.Bare(words![i]), meanings[i].Trim());
            }
        }

        return glosses;
    }

    public IReadOnlyList<string> UncoveredForms(Word word, StudyExampleSet examples)
    {
        return examples.EncounterForms
            .Where(form => !string.IsNullOrWhiteSpace(form))
            .Select(form => form.Trim().ToLowerInvariant())
            .Distinct()
            .Where(form => !examples.Examples.Any(e => Usable(e) && HeadwordText.Contains(e.Sentence, form)))
            // The headword itself is covered by any dictionary example that uses it
            .Where(form => !(string.Equals(form, word.Headword, StringComparison.OrdinalIgnoreCase)
                && DictionaryExample(word) is not null))
            .ToList();
    }

    /// <summary>
    /// The example to ask about next. One the word has not yet been answered on comes
    /// first - and before that, one using a form it has been met in - so each session moves
    /// on to a sentence it has not practised rather than one it already knows. Among those
    /// it has, the least practised and longest unused.
    /// </summary>
    private static StudyExample? ChooseExample(StudyExampleSet set)
    {
        var usable = set.Examples.Where(Usable).ToList();

        if (set.PreferredId is { } preferred && usable.FirstOrDefault(e => e.Id == preferred) is { } asked)
        {
            return asked;
        }

        var met = set.EncounterForms.Select(f => f.Trim().ToLowerInvariant()).ToHashSet();

        return usable
            .OrderBy(e => e.Successes > 0 ? 2 : met.Contains(e.Form.Trim().ToLowerInvariant()) ? 0 : 1)
            .ThenBy(e => e.Successes)
            .ThenBy(e => e.LastUsedAtUtc ?? DateTime.MinValue)
            .ThenBy(e => e.Id)
            .FirstOrDefault();
    }

    /// <summary>A sentence is only usable if the form it names is really in it.</summary>
    private static bool Usable(StudyExample example) =>
        !string.IsNullOrWhiteSpace(example.Sentence) && HeadwordText.Contains(example.Sentence, example.Form);

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
