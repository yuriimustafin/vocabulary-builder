using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Samples.Entities;

/// <summary>
/// Represents a list of vocabulary words/phrases for memorization
/// (e.g., synonyms for overused words, IELTS phrases, etc.)
/// </summary>
public class VocabularyList : BaseAuditableEntity
{
    /// <summary>
    /// Title/name of the list
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Language of the words/phrases in this list
    /// </summary>
    public Language Language { get; set; } = Language.English;
    
    /// <summary>
    /// Current status of the list
    /// </summary>
    public ListStatus Status { get; set; } = ListStatus.Active;
    
    /// <summary>
    /// Collection of items in this list
    /// </summary>
    public IList<VocabularyListItem> Items { get; private set; } = new List<VocabularyListItem>();
}
