using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.Imports;

/// <summary>
/// One run of one of the import pages, and what it brought in.
/// </summary>
/// <remarks>
/// Encounters already say which source a word was met in, but not which run: two LingQ
/// imports a month apart write encounters that look alike. This is the run - when, from
/// which file, under which tags - and its items are every term it was given and what became
/// of each, skipped ones included.
///
/// A word appears in every import that touched it, not only the one that created it, which
/// is what an item's <see cref="VocabularyImportItem.Outcome"/> distinguishes.
/// </remarks>
public class VocabularyImport : BaseAuditableEntity, IOwnedEntity
{
    public string OwnerId { get; set; } = string.Empty;

    public ImportKind Kind { get; set; }

    public Language Language { get; set; }

    /// <summary>The list name, book title or lesson name the import was given, if any.</summary>
    public string? Name { get; set; }

    public string? FileName { get; set; }

    /// <summary>
    /// The prefix of every encounter identifier this import wrote. Ties the import to its
    /// encounters, and is what the backfill grouped older encounters by.
    /// </summary>
    public string? SourceIdentifierBase { get; set; }

    public IList<string>? Tags { get; set; }

    public ImportStatus Status { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? Error { get; set; }

    /// <summary>
    /// True for an import put back together from encounters written before imports were
    /// recorded. Its grouping and dates are a best guess, and it knows nothing it skipped.
    /// </summary>
    public bool IsReconstructed { get; set; }

    /// <summary>Terms the source offered, before anything was made of them.</summary>
    public int TermsRead { get; set; }

    /// <summary>Distinct words the import created.</summary>
    public int WordsCreated { get; set; }

    /// <summary>Distinct words the import landed on, new or not.</summary>
    public int WordsTouched { get; set; }

    public int EncountersCreated { get; set; }

    public int TermsSkipped { get; set; }

    public IList<VocabularyImportItem> Items { get; private set; } = new List<VocabularyImportItem>();
}
