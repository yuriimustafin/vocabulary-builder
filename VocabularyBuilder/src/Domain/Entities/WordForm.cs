using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Samples.Entities;

/// <summary>
/// An inflected form of a word - a conjugated verb, most often - recorded so
/// that meeting "prends" or "pris" in a text can later be counted as an
/// encounter with "prendre".
/// </summary>
/// <remarks>
/// One row per cell of the source table, so a form that recurs is stored each time
/// ("prends" for both "je" and "tu") and the table can be shown as it was.
/// Deliberately not uniquely indexed on Form either: a single French form regularly
/// belongs to several lemmas ("suis" to both etre and suivre), and losing
/// either link would make encounter counting wrong.
/// </remarks>
public class WordForm : BaseAuditableEntity
{
    /// <summary>
    /// The word this form inflects from
    /// </summary>
    public int WordId { get; set; }
    public Word Word { get; set; } = null!;

    /// <summary>
    /// The inflected form as written, e.g. "prends"
    /// </summary>
    public required string Form { get; set; }

    /// <summary>
    /// Language of the form, denormalised from the word so that a lookup by
    /// written form does not have to join
    /// </summary>
    public Language Language { get; set; }

    /// <summary>
    /// Mood or grouping the form belongs to, as the source labels it,
    /// e.g. "indicatif", "subjonctif", "participe"
    /// </summary>
    public string? Mood { get; set; }

    /// <summary>
    /// Tense within the mood, e.g. "présent", "passé composé"
    /// </summary>
    public string? Tense { get; set; }

    /// <summary>
    /// Subject the form is given for, e.g. "je", "il, elle, on".
    /// Null for forms that have no subject, such as participles.
    /// </summary>
    public string? Person { get; set; }
}
