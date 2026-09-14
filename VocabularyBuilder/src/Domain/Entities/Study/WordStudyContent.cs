using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.Study;

/// <summary>
/// AI-generated study material for a word - strictly a gap filler.
/// A row exists only for words whose dictionary data was missing something an exercise
/// needs, and it stores only the fields that had to be generated. Definitions and examples
/// that already exist on <see cref="Word.Senses"/> or <see cref="Word.Examples"/> are read
/// from there and never copied here.
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
    /// When the current generation attempt was claimed. A claim older than the stale
    /// timeout is retried, so a crash mid-generation does not strand the word.
    /// </summary>
    public DateTime? ClaimedAtUtc { get; set; }

    public int GenerationAttempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>Prompt revision this content came from, so it can be regenerated selectively.</summary>
    public string PromptVersion { get; set; } = "v1";
}
