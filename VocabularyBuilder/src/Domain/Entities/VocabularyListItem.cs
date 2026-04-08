namespace VocabularyBuilder.Domain.Samples.Entities;

/// <summary>
/// Represents a single word or phrase in a vocabulary list
/// </summary>
public class VocabularyListItem : BaseAuditableEntity
{
    /// <summary>
    /// Reference to the parent list
    /// </summary>
    public int ListId { get; set; }
    
    /// <summary>
    /// The word or phrase text
    /// </summary>
    public required string Text { get; set; }
    
    /// <summary>
    /// Whether this item has been mastered/learned
    /// </summary>
    public bool IsMastered { get; set; } = false;
    
    /// <summary>
    /// Navigation property to parent list
    /// </summary>
    public VocabularyList List { get; set; } = null!;
}
