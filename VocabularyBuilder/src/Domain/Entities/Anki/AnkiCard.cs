using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Entities.Anki;
public class AnkiCard
{
    public long Id { get; set; }
    public long Nid { get; set; }
    public long Did { get; set; }
    public long Due { get; set; }
}
