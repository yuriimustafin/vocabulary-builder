using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Samples.Entities.ImportedBook;
public class BookInfo : BaseAuditableEntity
{
    public string? Title { get; set; }

    public string? Authors { get; set; }
}
