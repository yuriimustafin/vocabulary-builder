using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// Factory implementation for resolving word parsers based on language and dictionary source.
/// Parsers are discovered through their registration rather than named here, so
/// swapping one for a mock, or adding a dictionary, needs no change in this class.
/// </summary>
public class WordParserFactory : IWordParserFactory
{
    private readonly IReadOnlyDictionary<DictionarySourceType, IWordReferenceParser> _parsers;

    public WordParserFactory(IEnumerable<IWordReferenceParser> parsers)
    {
        // Last registration wins, which is how a mock replaces the real parser.
        _parsers = parsers
            .GroupBy(parser => parser.SourceType)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public IWordReferenceParser GetParser(Language language, DictionarySourceType sourceType)
    {
        // Honour an explicitly requested source first
        if (_parsers.TryGetValue(sourceType, out var parser))
        {
            return parser;
        }

        // Otherwise fall back to whichever dictionary the language defaults to
        if (_parsers.TryGetValue(language.GetDefaultSourceType(), out var defaultParser))
        {
            return defaultParser;
        }

        throw new InvalidOperationException(
            $"No parser is registered for {sourceType} or for the {language} default " +
            $"({language.GetDefaultSourceType()}). Registered: " +
            $"{string.Join(", ", _parsers.Keys)}.");
    }
}
