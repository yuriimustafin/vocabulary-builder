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
            // GPT rather than WordReference, which blocks the deployed host: every request
            // from the VPS comes back 418, so WordReference was answering for nobody in
            // production while still being asked first. See French support in CLAUDE.md.
            //
            // WordReference is deliberately still here - it remains the language's fallback
            // (LookupWordsFromDictionary.GetFallbackSourceType), its parser and recorded pages
            // are untouched, and a caller can still name it explicitly. It reads better than
            // the model where it is reachable: hand-written senses, and conjugation tables the
            // model is not asked for. Switching back is this line.
            Language.French => DictionarySourceType.Gpt,
            _ => DictionarySourceType.Oxford
        };
    }
}
