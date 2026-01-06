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

    /// <summary>
    /// Generates a unique identifier from the Kindle heading for idempotency.
    /// Format: "BookTitle:Headword:RawHeading" to ensure uniqueness per word per location
    /// Example: "Catching Fire:leached:Highlight(yellow) - 1 > Page 1 · Location 29"
    /// </summary>
    public string GetUniqueSourceIdentifier()
    {
        var bookTitle = Book?.Title ?? "Unknown";
        var heading = Heading ?? "NoHeading";
        
        // Use book title, headword, and the full heading text to ensure uniqueness
        // This handles cases where multiple words are on the same page
        return $"{bookTitle}:{TrimmedHeadword()}:{heading}";
    }
}
