using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Parsers;

/// <summary>
/// Result of parsing a word from a dictionary source, including HTML for caching
/// </summary>
public class WordParseResult
{
    public Word Word { get; set; } = null!;
    public string SourceHtml { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
}

public interface IWordReferenceParser
{
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

