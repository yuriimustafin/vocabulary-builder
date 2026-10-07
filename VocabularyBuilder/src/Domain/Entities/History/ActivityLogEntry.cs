using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.History;

/// <summary>
/// One thing that happened to a user's data: a word edited, a list deleted, an import run.
/// </summary>
/// <remarks>
/// Append-only, and deliberately without foreign keys to what it describes. The point of a
/// history is that it outlives its subject - a deleted word keeps the record of having been
/// deleted - so the word is named by id and by a copy of its headword, and nothing cascades
/// into here but the owner's own removal.
/// </remarks>
public class ActivityLogEntry : BaseEntity, IOwnedEntity
{
    public string OwnerId { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public ActivityAction Action { get; set; }

    public Language? Language { get; set; }

    /// <summary>The word acted on, if any. Kept when the word is gone.</summary>
    public int? WordId { get; set; }

    /// <summary>The word's headword at the time, so the entry reads without the word.</summary>
    public string? Headword { get; set; }

    /// <summary>The list acted on, when the action is about lists.</summary>
    public int? ListId { get; set; }

    public int? ImportId { get; set; }

    /// <summary>One line for the history page: "New → Learned".</summary>
    public string? Summary { get; set; }

    /// <summary>Whatever else is worth keeping, as JSON: the fields an edit changed, a sweep's counts.</summary>
    public string? Details { get; set; }
}
