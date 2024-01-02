using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VocabularyBuilder.Domain.Entities.ImportedBook;
public class ImportedBookWord : BaseAuditableEntity
{
    public required string Headword { get; set; }

    public int Page { get; set; }

    public string? Note { get; set; }

    public Chapter? Chapter { get; set;}

    public BookInfo? Book { get; set;}
}
