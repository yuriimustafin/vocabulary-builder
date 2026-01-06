using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Samples.Entities;
public class Word : BaseAuditableEntity
{
    // TODO: Consider change PK from Id to Headword
    // TODO: Consider renaming/using instead Lemma
    public required string Headword { get; set; }
    public string? Transcription { get; set; }

    // TODO: Change it to enum
    public string? PartOfSpeech{ get; set; }
    public IList<Sense>? Senses { get; set; }
    public IList<string>? Examples { get; set; }
    public int? Frequency { get; set; }
    
    /// <summary>
    /// Collection of all encounters/additions of this word from various sources
    /// </summary>
    public ICollection<WordEncounter> WordEncounters { get; set; } = new List<WordEncounter>();

    /// <summary>
    /// Computed property to get the total encounter count
    /// </summary>
    public int EncounterCount => WordEncounters?.Count ?? 0;

    public string GetHeadword()
    {
        var prefix = (PartOfSpeech is not null && PartOfSpeech.ToLower().Contains("verb")) 
            ? "to " 
            : "";
        return prefix + Headword;
    }
}
