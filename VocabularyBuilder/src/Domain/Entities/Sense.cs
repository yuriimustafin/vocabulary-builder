using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Samples.Entities;
public class Sense : BaseAuditableEntity
{
    public required string Definition { get; set; }
    public required IEnumerable<string> Examples { get; set; }
}
