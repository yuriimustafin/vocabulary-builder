using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Entities.Imports;

/// <summary>
/// One term an import was given, and the word it became - or why it became none.
/// </summary>
public class VocabularyImportItem : BaseEntity
{
    public int ImportId { get; set; }

    public VocabularyImport Import { get; set; } = null!;

    /// <summary>
    /// The word the term landed on. Cleared, not cascaded, when the word is deleted: the
    /// import still brought it in, and <see cref="Headword"/> still says what it was.
    /// </summary>
    public int? WordId { get; set; }

    public Word? Word { get; set; }

    /// <summary>The headword the term was stored under, empty for a skipped term.</summary>
    public string Headword { get; set; } = string.Empty;

    /// <summary>The term as the source wrote it: "Vous allez", "une randonnée".</summary>
    public string SourceTerm { get; set; } = string.Empty;

    public ImportItemOutcome Outcome { get; set; }

    /// <summary>Whether this term added an encounter - false when a re-import met it again.</summary>
    public bool EncounterAdded { get; set; }

    /// <summary>Why a skipped term was set aside: "Sentence", "Question", "Expression".</summary>
    public string? Reason { get; set; }
}
