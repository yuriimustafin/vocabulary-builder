using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.Study;

/// <summary>
/// AI-generated study material for a word.
///
/// The definition and the single context sentence are gap fillers: generated only when the
/// dictionary data had none, and read from <see cref="Word.Senses"/> or
/// <see cref="Word.Examples"/> otherwise. The rest - what the word is used about, where it
/// comes from, words it shares that origin with, and a sound-alike to hang it on - is
/// generated for every word. Each is another route back to the word, and a word reached
/// by many routes is the one that is remembered. Example sentences live in
/// <see cref="StudyExample"/>.
/// </summary>
public class WordStudyContent : BaseAuditableEntity
{
    public int WordId { get; set; }

    public Word Word { get; set; } = null!;

    public StudyContentStatus Status { get; set; } = StudyContentStatus.Pending;

    /// <summary>Generated only when the word has no senses to take a definition from.</summary>
    public string? GeneratedDefinition { get; set; }

    /// <summary>Generated only when no existing example sentence contains the headword.</summary>
    public string? GeneratedContextSentence { get; set; }

    /// <summary>
    /// What the word is typically used about, in one line: for "bright", light, colours, a
    /// promising future, a clever idea. Says what the meaning covers where a definition
    /// only says what it is.
    /// </summary>
    public string? Usage { get; set; }

    /// <summary>A line on where the word comes from, when that is known rather than guessed.</summary>
    public string? Etymology { get; set; }

    /// <summary>
    /// Words in English that share the origin - "prendre: apprehend, comprehend" - with any
    /// false friend flagged. None for an English word.
    /// </summary>
    public string? Cognates { get; set; }

    /// <summary>
    /// Similar-sounding English or Ukrainian words and a short scene tying them to the
    /// meaning. Support for the first retrievals, not something to be tested on.
    /// </summary>
    public string? Mnemonic { get; set; }

    /// <summary>
    /// When the current generation attempt was claimed. A claim older than the stale
    /// timeout is retried, so a crash mid-generation does not strand the word.
    /// </summary>
    public DateTime? ClaimedAtUtc { get; set; }

    public int GenerationAttempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>
    /// The import that reopened this content by bringing the word in a form no example used.
    /// The generation that follows is that import's doing, and its model call is filed under
    /// it. Cleared once the content is ready again.
    /// </summary>
    public int? ReopenedByImportId { get; set; }

    /// <summary>
    /// Prompt revision this content came from, so it can be regenerated selectively. Set
    /// only when a generation succeeds, so a word whose content predates the current prompt
    /// is asked again once for what the new one adds.
    /// </summary>
    public string PromptVersion { get; set; } = "v1";
}
