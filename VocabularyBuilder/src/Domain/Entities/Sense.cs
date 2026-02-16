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
    public required IList<string> Examples { get; set; }
}
