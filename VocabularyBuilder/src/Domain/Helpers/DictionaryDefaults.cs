using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Helpers;

/// <summary>
/// Which dictionary a language is looked up in when the caller has not asked
/// for a specific one. Kept in one place so a language's default source can be
/// changed without hunting through callers.
/// </summary>
public static class DictionaryDefaults
{
    public static DictionarySourceType GetDefaultSourceType(this Language language)
    {
        return language switch
        {
            Language.French => DictionarySourceType.WordReference,
            _ => DictionarySourceType.Oxford
        };
    }
}
