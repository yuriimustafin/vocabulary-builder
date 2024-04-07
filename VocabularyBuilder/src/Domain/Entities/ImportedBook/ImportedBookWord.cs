using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Domain.Samples.Entities.ImportedBook;
public class ImportedBookWord : BaseAuditableEntity
{
    public required string Headword { get; set; }

    public string? Heading { get; set; }

    public string? Note { get; set; }

    public Chapter? Chapter { get; set;}

    public BookInfo? Book { get; set;}

    public ImportWordStatus? Status{ get; set; }

    public int? WordId { get; set; }
    
    public Word? Word { get; set; }

    public string TrimmedHeadword()
    {
        return Headword.Trim().Trim('"', '\'', '.', ',', ';', ':', '/', '-', '!', '?').Trim().ToLower();
    }

    public int ExtractDigitsFromHeading() => StringHelper.ExtractDigits(Heading);
}
