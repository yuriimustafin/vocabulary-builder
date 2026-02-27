using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.Frequency;
public class FrequencyWord : BaseAuditableEntity
{
    public required string Headword { get; set; }
    public int? Frequency { get; set; }
    
    /// <summary>
    /// Language of the word (English, French, etc.)
    /// </summary>
    public Language Language { get; set; } = Language.English;
    public int? BaseFormId { get; set; }
    public FrequencyWord? BaseForm { get; set; }
    public IList<FrequencyWord>? DerivedForms { get; set; }
}
