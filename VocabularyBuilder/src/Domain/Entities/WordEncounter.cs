using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Samples.Entities;

/// <summary>
/// Represents a record of encountering a word from a specific source.
/// Provides idempotency by tracking source and sourceIdentifier.
/// </summary>
public class WordEncounter : BaseAuditableEntity
{
    /// <summary>
    /// Reference to the Word entity
    /// </summary>
    public int WordId { get; set; }
    public Word Word { get; set; } = null!;

    /// <summary>
    /// Source where the word was encountered (Kindle, Manual entry, etc.)
    /// </summary>
    public WordEncounterSource Source { get; set; }

    /// <summary>
    /// Unique identifier for the source to provide idempotency.
    /// Examples:
    /// - For Kindle: "BookId:Page" or "BookId:Location"
    /// - For Oxford list: "ListId:LineNumber" or "ListName:WordIndex"
    /// - For Manual/API: Generated GUID or user session + timestamp
    /// </summary>
    public string? SourceIdentifier { get; set; }

    /// <summary>
    /// Additional context about the encounter (e.g., book title, list name, etc.)
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Optional notes associated with this encounter
    /// </summary>
    public string? Notes { get; set; }
}
