namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// Status of a vocabulary list
/// </summary>
public enum ListStatus
{
    /// <summary>
    /// List is actively being worked on
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// List is completed/mastered
    /// </summary>
    Completed = 1,
    
    /// <summary>
    /// List is archived (not actively used)
    /// </summary>
    Archived = 2
}
