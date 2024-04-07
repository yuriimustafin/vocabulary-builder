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
    public IList<Sense>? Senses { get; set; }
    public IList<string>? Examples { get; set; }
    public int? Frequency { get; set; }
    public int EncounterCount { get; set; }
}
