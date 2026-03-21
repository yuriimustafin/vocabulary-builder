using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Parsers;

/// <summary>
/// Factory for resolving the appropriate word parser based on language and dictionary source
/// </summary>
public interface IWordParserFactory
{
    /// <summary>
    /// Get the appropriate parser for the specified language and source type
    /// </summary>
    IWordReferenceParser GetParser(Language language, DictionarySourceType sourceType);
}
