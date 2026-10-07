namespace VocabularyBuilder.Domain.Entities.Study;

/// <summary>
/// One example sentence for a word, built around one of the words it is typically used
/// with - "a bright future", "bright colours" - so that together a word's examples show
/// the range of what it means.
///
/// Recognition and cloze exercises are asked from these. Each records how often it has
/// been answered correctly, so a word comes round again on a sentence it has not yet
/// been asked about - and first of all on one using a form of the word that has been met
/// in reading but not practised.
/// </summary>
public class StudyExample : BaseAuditableEntity
{
    public int WordId { get; set; }

    public Word Word { get; set; } = null!;

    /// <summary>The sentence, in the language being learned.</summary>
    public string Sentence { get; set; } = string.Empty;

    /// <summary>What it says, in the learner's language.</summary>
    public string? Translation { get; set; }

    /// <summary>
    /// The word exactly as it appears in the sentence - "prend" in an example for
    /// "prendre". This is what a cloze blanks out.
    /// </summary>
    public string Form { get; set; } = string.Empty;

    /// <summary>The phrase the sentence is built around, such as "a bright future".</summary>
    public string? Collocation { get; set; }

    /// <summary>
    /// Each word of the sentence as written, paired by position with <see cref="GlossTranslations"/>:
    /// what the pieces of a sentence mean, for the hint on one rebuilt from them.
    /// </summary>
    public IList<string>? GlossWords { get; set; }

    /// <summary>What each of <see cref="GlossWords"/> means in that sentence, in the learner's language.</summary>
    public IList<string>? GlossTranslations { get; set; }

    /// <summary>Times an exercise built on this sentence was answered correctly.</summary>
    public int Successes { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }
}
