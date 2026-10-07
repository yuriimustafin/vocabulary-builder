namespace VocabularyBuilder.Domain.Enums;

/// <summary>Which of the import pages an import came from.</summary>
public enum ImportKind
{
    /// <summary>Pasted words or dictionary URLs, from Bulk Import.</summary>
    BulkList = 0,

    Kindle = 1,

    LingQ = 2,

    LessonNotes = 3
}
