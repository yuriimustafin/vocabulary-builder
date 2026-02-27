using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Samples.Entities.ImportedBook;
public class ImportedBookWord : BaseAuditableEntity
{
    public required string Headword { get; set; }
    
    /// <summary>
    /// Language of the word (English, French, etc.)
    /// </summary>
    public Language Language { get; set; } = Language.English;

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
    /// Format: "BookTitle:Headword:PageNumber" to ensure uniqueness per word per page
    /// Example: "Harry Potter and the Goblet of Fire:gnarled:6"
    /// </summary>
    public string GetUniqueSourceIdentifier()
    {
        var bookTitle = Book?.Title ?? "Unknown";
        var pageNumber = StringHelper.ExtractPageNumber(Heading);
        
        // Use book title, headword, and page number to ensure uniqueness
        return $"{bookTitle}:{TrimmedHeadword()}:{pageNumber}";
    }
}
