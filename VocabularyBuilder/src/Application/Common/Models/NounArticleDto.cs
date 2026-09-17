using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Application.Common.Models;

/// <summary>
/// The article to show with a noun, and the gender to colour it by. Absent for anything
/// that is not a noun of known gender, so the client can render a bare headword.
/// </summary>
public class NounArticleDto
{
    /// <summary>"le", "la", "l'", "les", or "le/la"</summary>
    public string Definite { get; init; } = string.Empty;

    /// <summary>"un", "une", "des", or "un/une"</summary>
    public string Indefinite { get; init; } = string.Empty;

    /// <summary>"masculine", "feminine" or "common" - lower case, as a style hook for the client.</summary>
    public string Gender { get; init; } = string.Empty;

    /// <summary>"l'" is written against the word, with no space.</summary>
    public bool IsElided { get; init; }

    public bool IsPlural { get; init; }

    public static NounArticleDto? From(NounArticle? article)
    {
        if (article is null)
        {
            return null;
        }

        return new NounArticleDto
        {
            Definite = article.Definite,
            Indefinite = article.Indefinite,
            Gender = article.Gender.ToString().ToLowerInvariant(),
            IsElided = article.IsElided,
            IsPlural = article.IsPlural
        };
    }
}
