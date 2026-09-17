namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// WordReference parsing settings, bound from the "WordReference" section of
/// appsettings.json.
/// </summary>
public class WordReferenceOptions
{
    public const string SectionName = "WordReference";

    /// <summary>
    /// How many entries to keep from the Principal Translations section.
    /// WordReference lists 15 for "prendre" alone, which is far more than
    /// belongs on a card.
    /// </summary>
    public int MaxTranslations { get; set; } = 5;

    /// <summary>
    /// Fetch and store the conjugation table for verbs.
    /// </summary>
    public bool IncludeConjugations { get; set; } = true;

    /// <summary>
    /// Pause between requests, so that importing a long list stays polite.
    /// </summary>
    public int RequestDelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Serve pre-recorded pages from MockData/wordreference instead of the
    /// network. Used by the E2E tests.
    /// </summary>
    public bool UseMockMode { get; set; }

    /// <summary>
    /// WordReference returns an empty page to clients that do not look like a
    /// browser.
    /// </summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
}
