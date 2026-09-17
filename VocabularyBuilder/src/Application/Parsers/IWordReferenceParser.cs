using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Parsers;

/// <summary>
/// Result of parsing a word from a dictionary source, including HTML for caching
/// </summary>
public class WordParseResult
{
    public Word Word { get; set; } = null!;

    /// <summary>
    /// The term that was looked up. Set by the parser so that callers do not
    /// have to pair results back to inputs by position - a word the dictionary
    /// does not have simply produces no result, which shifts every later index.
    /// </summary>
    public string SearchedTerm { get; set; } = string.Empty;

    public string SourceHtml { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Inflected forms of the word, where the source publishes them.
    /// Empty for words that do not inflect, and for dictionaries that do not
    /// provide conjugations.
    /// </summary>
    public IReadOnlyList<WordForm> Forms { get; set; } = Array.Empty<WordForm>();

    /// <summary>
    /// Raw conjugation page, cached alongside the entry so the forms can be
    /// re-derived without another request.
    /// </summary>
    public string? ConjugationHtml { get; set; }

    public string? ConjugationUrl { get; set; }
}

public interface IWordReferenceParser
{
    /// <summary>
    /// The dictionary this parser reads. Used by IWordParserFactory to route a
    /// lookup, so a parser only has to be registered to become available.
    /// </summary>
    DictionarySourceType SourceType { get; }

    // TODO: change return type to DictWord
    Task<IEnumerable<Word>> GetWords(IEnumerable<string> searchedWords);
    
    /// <summary>
    /// Get words with their source HTML and URL for caching
    /// </summary>
    Task<IEnumerable<WordParseResult>> GetWordsWithSource(IEnumerable<string> searchedWords);
    
    /// <summary>
    /// Parse word from cached HTML content
    /// </summary>
    Task<Word?> GetWordFromCachedHtml(string cachedHtml);
}

