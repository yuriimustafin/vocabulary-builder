using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Infrastructure.Exporters;

/// <summary>
/// Anki export settings, bound from the "Anki" section of appsettings.json.
/// </summary>
public class AnkiExportOptions
{
    public const string SectionName = "Anki";

    /// <summary>
    /// Deck name per language, keyed by the <see cref="Language"/> name
    /// ("English", "French"). Languages absent from this map fall back to
    /// <see cref="DeckNameFormat"/>.
    /// </summary>
    public Dictionary<string, string> DeckNames { get; set; } = new();

    /// <summary>
    /// Template used for any language without an explicit entry in
    /// <see cref="DeckNames"/>. {0} is the language name.
    /// </summary>
    public string DeckNameFormat { get; set; } = "{0}::Vocabulary";

    /// <summary>
    /// Resolve the deck a word belongs to. Resolved per word rather than per
    /// export, so a mixed-language export still lands in the right decks.
    /// </summary>
    public string GetDeckName(Language language)
    {
        var languageName = language.ToString();

        return DeckNames.TryGetValue(languageName, out var deckName) && !string.IsNullOrWhiteSpace(deckName)
            ? deckName
            : string.Format(DeckNameFormat, languageName);
    }
}
