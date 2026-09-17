using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Samples.Entities;
public class Sense : BaseAuditableEntity
{
    public required string Definition { get; set; }
    public PartsOfSpeech PartOfSpeech { get; set; }

    /// <summary>Gender of this meaning, for a noun in a language that has one.</summary>
    public GrammaticalGender? Gender { get; set; }

    /// <summary>True when this meaning only exists in the plural.</summary>
    public bool IsPluralOnly { get; set; }
    public required IList<string> Examples { get; set; }
}
