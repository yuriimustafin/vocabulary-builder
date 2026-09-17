using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Parsers;

/// <summary>
/// Reads the inflected forms of a word out of a conjugation page.
/// </summary>
public interface IConjugationParser
{
    /// <summary>
    /// Parse every inflected form found in the page. The forms are not attached
    /// to a word - the caller sets WordId once the word is known.
    /// </summary>
    Task<IReadOnlyList<WordForm>> GetFormsAsync(string html);
}
