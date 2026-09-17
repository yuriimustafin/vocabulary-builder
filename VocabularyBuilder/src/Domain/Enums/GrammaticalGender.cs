namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// Grammatical gender of a noun. Only meaningful for languages that have it - an
/// English noun leaves it unset.
/// </summary>
public enum GrammaticalGender
{
    Masculine = 1,
    Feminine = 2,

    /// <summary>
    /// Takes either article: nouns naming people of either sex ("un/une élève") and the
    /// few whose gender simply varies ("un/une après-midi").
    /// </summary>
    Common = 3
}
