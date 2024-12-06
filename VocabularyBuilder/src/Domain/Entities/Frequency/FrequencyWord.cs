﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Entities.Frequency;
public class FrequencyWord : BaseAuditableEntity
{
    public required string Headword { get; set; }
    public int Frequency { get; set; }
    public int LemmaId { get; set; }
    public FrequencyWord? Lemma { get; set; }
    public IList<FrequencyWord>? DerivedForms { get; set; }
}
