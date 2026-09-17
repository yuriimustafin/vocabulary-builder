namespace VocabularyBuilder.Domain.Enums;

public enum WordEncounterSource
{
    Manual = 0,
    KindleHighlights = 1,
    OxfordDictionaryList = 2,
    ImportedFile = 3,
    Api = 4,

    /// <summary>A term exported from LingQ.</summary>
    LingQ = 5,

    /// <summary>Vocabulary taken from notes written during a lesson.</summary>
    LessonNotes = 6
}
