using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.History;

/// <summary>
/// One request made to a model or a dictionary site, whether it succeeded or not.
/// </summary>
/// <remarks>
/// A model call keeps its prompt and its answer in full: that is the only record of what was
/// actually asked and said, since a word's cached source keeps just the first answer it was
/// ever given. A dictionary page keeps only its URL, status and size - the page itself is
/// already cached against the word, and at hundreds of kilobytes each it is not worth storing
/// again per request.
///
/// Like the activity log it points at words and imports by id only, and survives them.
/// </remarks>
public class ExternalCallLog : BaseEntity, IOwnedEntity
{
    public string OwnerId { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public int DurationMs { get; set; }

    public ExternalCallProvider Provider { get; set; }

    public ExternalCallPurpose Purpose { get; set; }

    /// <summary>The model name, for a model call.</summary>
    public string? Model { get; set; }

    /// <summary>What the call was about - usually the word looked up.</summary>
    public string? Target { get; set; }

    public string? Url { get; set; }

    /// <summary>The prompt, for a model call.</summary>
    public string? Request { get; set; }

    /// <summary>The answer, for a model call.</summary>
    public string? Response { get; set; }

    /// <summary>Size of the answer in characters, kept for every call.</summary>
    public int? ResponseLength { get; set; }

    public int? StatusCode { get; set; }

    public bool Succeeded { get; set; }

    public string? Error { get; set; }

    public int? PromptTokens { get; set; }

    public int? CompletionTokens { get; set; }

    /// <summary>True when a recorded response answered instead of the real service.</summary>
    public bool IsMock { get; set; }

    public int? WordId { get; set; }

    public int? ImportId { get; set; }
}
