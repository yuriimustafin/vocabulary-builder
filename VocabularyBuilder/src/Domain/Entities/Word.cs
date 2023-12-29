using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Entities;
public class Word
{
    public required string Headword { get; set; }
    public required IEnumerable<Sense> Senses { get; set; }
    public IEnumerable<string>? Examples { get; set; }
}
