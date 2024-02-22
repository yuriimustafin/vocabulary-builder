using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Entities;
public class Word : BaseAuditableEntity
{
    public required string Headword { get; set; }
    public IList<Sense>? Senses { get; set; }
    public IList<string>? Examples { get; set; }
    public int? Frequency { get; set; }
}
