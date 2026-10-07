namespace VocabularyBuilder.Domain.Enums;

/// <summary>What an outbound request was made for.</summary>
public enum ExternalCallPurpose
{
    Other,
    DictionaryEntry,
    Conjugation,
    StudyContent,
    NotesExtraction,
    LemmaResolution,
    ListGeneration,
    AudioText
}
