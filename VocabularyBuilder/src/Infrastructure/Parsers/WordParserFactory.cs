using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// Factory implementation for resolving word parsers based on language and dictionary source
/// </summary>
public class WordParserFactory : IWordParserFactory
{
    private readonly IWordReferenceParser _oxfordParser;
    private readonly IWordReferenceParser _gptFrenchParser;

    public WordParserFactory(
        OxfordParser oxfordParser,
        GptFrenchParser gptFrenchParser)
    {
        _oxfordParser = oxfordParser;
        _gptFrenchParser = gptFrenchParser;
    }

    public IWordReferenceParser GetParser(Language language, DictionarySourceType sourceType)
    {
        // Route based on explicit source type first
        if (sourceType == DictionarySourceType.Gpt)
        {
            return _gptFrenchParser;
        }

        if (sourceType == DictionarySourceType.Oxford)
        {
            return _oxfordParser;
        }

        // If no explicit source type, route based on language
        return language switch
        {
            Language.English => _oxfordParser,
            Language.French => _gptFrenchParser,
            _ => _oxfordParser // Default fallback
        };
    }
}
