using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Ai;

/// <summary>
/// What a model made of one term: the headword it reduces to, or the reason it is not a
/// vocabulary item at all.
/// </summary>
/// <param name="SourceTerm">The term as it was asked about, so callers can pair the answer back.</param>
/// <param name="Lemma">The dictionary headword, bare of its article. Null when skipped.</param>
/// <param name="SkipReason">Why it is not a vocabulary item. Null when a lemma was found.</param>
public record AnalyzedTerm(string SourceTerm, string? Lemma, string? SkipReason);

/// <summary>
/// The model-backed half of import parsing: splitting free-form notes into items, and
/// reducing the terms <see cref="Domain.Helpers.FrenchTermNormalizer"/> could not settle
/// by rule alone.
/// </summary>
public interface IVocabularyAnalyzer
{
    /// <summary>
    /// Breaks lesson notes into individual vocabulary items, dropping the translations
    /// they are written with and separating items that share a line.
    /// </summary>
    Task<IReadOnlyList<string>> ExtractItemsAsync(
        string notes,
        Language language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reduces terms to their headwords - a conjugated verb to its infinitive, a modified
    /// noun to the noun - and marks the ones that turn out to be phrases rather than words.
    /// </summary>
    Task<IReadOnlyList<AnalyzedTerm>> ResolveLemmasAsync(
        IReadOnlyList<string> terms,
        Language language,
        CancellationToken cancellationToken = default);
}
