using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Domain.Samples.Entities;
public class Word : BaseAuditableEntity
{
    // TODO: Consider change PK from Id to Headword
    // TODO: Consider renaming/using instead Lemma
    public required string Headword { get; set; }
    public string? Transcription { get; set; }
    
    /// <summary>
    /// Language of the word (English, French, etc.)
    /// </summary>
    public Language Language { get; set; } = Language.English;

    // TODO: Change it to enum
    public string? PartOfSpeech{ get; set; }

    /// <summary>
    /// Gender of the word's primary meaning, when that meaning is a noun. A noun whose
    /// meanings differ in gender ("le livre", "la livre") keeps each one on its sense.
    /// </summary>
    public GrammaticalGender? Gender { get; set; }

    /// <summary>
    /// True for nouns that only exist in the plural ("les gens", "les vacances").
    /// </summary>
    public bool IsPluralOnly { get; set; }
    public IList<Sense>? Senses { get; set; }
    public IList<string>? Examples { get; set; }

    /// <summary>
    /// Translations of <see cref="Examples"/>, paired by position. See the same field on
    /// <see cref="Sense"/> for why they are kept apart.
    /// </summary>
    public IList<string>? ExampleTranslations { get; set; }

    /// <summary>
    /// Free-form labels the word was collected under - the lesson, the book, the export it
    /// came from. Added to rather than replaced: a word met again under a second tag keeps
    /// the first, so the tags accumulate into a record of where it has been seen.
    /// </summary>
    public IList<string>? Tags { get; set; }
    public int? Frequency { get; set; }
    public WordStatus Status { get; set; } = WordStatus.New;
    
    /// <summary>
    /// Unique identifier for syncing with external systems (Anki, etc.)
    /// Generated when the word is first exported
    /// </summary>
    public Guid? SyncId { get; set; }
    
    /// <summary>
    /// User-set flag queueing this word for the next study session, independent of
    /// <see cref="Status"/> (which belongs to the export pipeline).
    /// Cleared automatically once the word is introduced.
    /// </summary>
    public bool IsMarkedForStudy { get; set; }

    /// <summary>
    /// Collection of all encounters/additions of this word from various sources
    /// </summary>
    public ICollection<WordEncounter> WordEncounters { get; set; } = new List<WordEncounter>();
    
    /// <summary>
    /// Collection of cached dictionary sources for this word (Oxford, Webster, etc.)
    /// </summary>
    public ICollection<WordDictionarySource> DictionarySources { get; set; } = new List<WordDictionarySource>();

    /// <summary>
    /// Computed property to get the total encounter count
    /// </summary>
    public int EncounterCount => WordEncounters?.Count ?? 0;

    /// <summary>
    /// The headword as it should be presented on a card. English verbs are
    /// shown in the "to run" citation form; French infinitives stand alone,
    /// so the particle is only ever added for English.
    /// </summary>
    /// <summary>
    /// The article the word is learned with, or null when it is not a French noun of
    /// known gender.
    /// </summary>
    public NounArticle? GetArticle() =>
        Language == Language.French
            ? FrenchArticles.For(Headword, Gender, IsPluralOnly, Transcription)
            : null;

    /// <summary>
    /// The article for one particular meaning, which can differ from the word's own.
    /// </summary>
    public NounArticle? GetArticle(Sense sense) =>
        Language == Language.French
            ? FrenchArticles.For(Headword, sense.Gender, sense.IsPluralOnly, Transcription)
            : null;

    /// <summary>
    /// Whether the word is still missing something only a dictionary supplies.
    /// </summary>
    /// <remarks>
    /// Gender is asked about separately from the rest: a word can pick up a definition from
    /// a model and still have no gender, and without one <see cref="FrenchArticles"/> will
    /// not guess an article, so the noun is shown bare. Only nouns are considered for it,
    /// read from the part of speech as the dictionary writes it ("nf", "nm").
    /// </remarks>
    public bool IsMissingDictionaryData()
    {
        if (string.IsNullOrWhiteSpace(PartOfSpeech) || Senses is null || !Senses.Any())
        {
            return true;
        }

        var isNoun = PartOfSpeech.Trim().StartsWith("n", StringComparison.OrdinalIgnoreCase);

        return Language == Language.French && isNoun && Gender is null;
    }

    public string GetHeadword()
    {
        var prefix = (Language == Language.English
                && PartOfSpeech is not null 
                && PartOfSpeech.ToLower().Contains("verb")
                && !PartOfSpeech.ToLower().Contains("adverb")) 
            ? "to " 
            : "";
        return prefix + Headword;
    }
}
