namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// Types of dictionary sources used for parsing word definitions
/// </summary>
public enum DictionarySourceType
{
    /// <summary>
    /// Oxford Learners Dictionary
    /// </summary>
    Oxford = 0,
    
    /// <summary>
    /// Merriam-Webster Dictionary
    /// </summary>
    MerriamWebster = 1,
    
    /// <summary>
    /// Cambridge Dictionary
    /// </summary>
    Cambridge = 2,
    
    /// <summary>
    /// Other or custom dictionary source
    /// </summary>
    Other = 99
}
