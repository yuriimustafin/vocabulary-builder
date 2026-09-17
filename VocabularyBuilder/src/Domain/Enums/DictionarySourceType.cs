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
    /// GPT-based AI dictionary (for languages without traditional dictionary support)
    /// </summary>
    Gpt = 3,
    
    /// <summary>
    /// WordReference bilingual dictionary (wordreference.com)
    /// </summary>
    WordReference = 4,
    
    /// <summary>
    /// WordReference conjugation tables. Held separately from
    /// <see cref="WordReference"/> because it is a second page for the same
    /// word, and a word may only cache one document per source type.
    /// </summary>
    WordReferenceConjugation = 5,
    
    /// <summary>
    /// Other or custom dictionary source
    /// </summary>
    Other = 99
}
