using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Samples.Entities;

/// <summary>
/// Stores cached HTML content from dictionary sources for a word
/// Allows multiple dictionary sources per word for comparison and redundancy
/// </summary>
public class WordDictionarySource : BaseAuditableEntity
{
    /// <summary>
    /// Foreign key to the Word entity
    /// </summary>
    public int WordId { get; set; }
    
    /// <summary>
    /// Navigation property to the Word entity
    /// </summary>
    public Word Word { get; set; } = null!;
    
    /// <summary>
    /// Type of dictionary source (Oxford, Webster, etc.)
    /// </summary>
    public DictionarySourceType SourceType { get; set; }
    
    /// <summary>
    /// Cached HTML content from the dictionary page
    /// Stored to avoid re-fetching the same page
    /// </summary>
    public string SourceHtml { get; set; } = string.Empty;
    
    /// <summary>
    /// Original URL where the HTML was fetched from
    /// </summary>
    public string? SourceUrl { get; set; }
}
