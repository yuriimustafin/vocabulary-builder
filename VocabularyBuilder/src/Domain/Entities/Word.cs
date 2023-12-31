using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Entities;
public class Word : BaseAuditableEntity
{
    public required string Headword { get; set; }
    public IEnumerable<Sense>? Senses { get; set; }
    public IEnumerable<string>? Examples { get; set; }
}
