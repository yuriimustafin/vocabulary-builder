using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Enums;

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
    public IList<Sense>? Senses { get; set; }
    public IList<string>? Examples { get; set; }
    public int? Frequency { get; set; }
    public WordStatus Status { get; set; } = WordStatus.New;
    
    /// <summary>
    /// Unique identifier for syncing with external systems (Anki, etc.)
    /// Generated when the word is first exported
    /// </summary>
    public Guid? SyncId { get; set; }
    
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

    public string GetHeadword()
    {
        var prefix = (PartOfSpeech is not null 
                && PartOfSpeech.ToLower().Contains("verb")
                && !PartOfSpeech.ToLower().Contains("adverb")) 
            ? "to " 
            : "";
        return prefix + Headword;
    }
}
