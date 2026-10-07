namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// Something a user did, or had done on their behalf, that changed their data.
/// </summary>
/// <remarks>
/// Stored by name rather than by number, so the log stays readable in SQL and survives
/// members being reordered. A member may be added freely; renaming one orphans the rows
/// already written under the old name.
/// </remarks>
public enum ActivityAction
{
    WordCreated,
    WordUpdated,
    WordStatusChanged,
    WordDeleted,
    WordMarkedForStudy,
    WordUnmarkedForStudy,
    WordMarkedKnown,
    WordFilledFromDictionary,
    WordNotFoundInDictionary,

    DictionaryFillRun,
    CachedSensesReparsed,
    FrequenciesUpdated,
    WordsExported,

    ImportCompleted,
    ImportFailed,
    FrequencyDataImported,

    CardSuspended,
    CardResumed,
    CardReset,
    StudyProgressCleared,

    ListCreated,
    ListUpdated,
    ListDeleted,
    ListItemAdded,
    ListItemUpdated,
    ListItemDeleted
}
