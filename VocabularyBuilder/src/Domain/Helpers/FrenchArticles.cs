using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Domain.Helpers;

/// <summary>
/// The article a French noun is learned with.
/// </summary>
/// <param name="Definite">"le", "la", "l'", "les", or "le/la" for a noun of either gender.</param>
/// <param name="Indefinite">"un", "une", "des" or "un/une".</param>
/// <param name="IsElided">
/// True for "l'". The elided and plural definite articles hide the gender, which is why
/// the indefinite one is kept alongside.
/// </param>
public record NounArticle(
    string Definite,
    string Indefinite,
    GrammaticalGender Gender,
    bool IsElided,
    bool IsPlural)
{
    /// <summary>"la maison", "l'arbre", "les gens" - no space after an elided article.</summary>
    public string WithDefinite(string headword) =>
        IsElided ? Definite + headword : $"{Definite} {headword}";

    public string WithIndefinite(string headword) => $"{Indefinite} {headword}";
}

/// <summary>
/// Chooses the article for a French noun: gender and number decide le / la / les, and the
/// sound the word opens with decides whether the singular elides to l'.
/// </summary>
public static class FrenchArticles
{
    /// <summary>
    /// Dictionaries mark an aspirated h, which blocks elision, with a stress-like tick at
    /// the start of the transcription: hache [ˈaʃ] takes "la", homme [ɔm] takes "l'".
    /// WordReference uses U+02C8; an apostrophe is accepted as the older typewriter form.
    /// </summary>
    private static readonly char[] DisjunctionMarks = { 'ˈ', '\'' };

    private const string Vowels = "aàâäeéèêëiîïoôöuùûüœæ";

    /// <summary>
    /// Common nouns with an aspirated h, for when there is no transcription to read the
    /// mark from - a word imported as text, or parsed by a source that does not mark it.
    /// Deliberately limited to the uncontested ones; "hyène", which takes either, is left out.
    /// </summary>
    private static readonly HashSet<string> AspiratedH = new(StringComparer.OrdinalIgnoreCase)
    {
        "hache", "haie", "haillon", "haine", "hall", "halle", "halo", "halte", "hamac",
        "hamburger", "hameau", "hamster", "hanche", "handball", "handicap", "hangar",
        "hanneton", "harangue", "haras", "hardiesse", "harem", "hareng", "hargne", "haricot",
        "harnais", "harpe", "hasard", "hâte", "hausse", "haut", "hauteur", "havre", "hayon",
        "hérisson", "hernie", "héron", "héros", "herse", "hêtre", "heurt", "hibou", "hiérarchie",
        "hippie", "hobby", "hockey", "hold-up", "homard", "honte", "hoquet", "horde", "hotte",
        "houblon", "houille", "houle", "housse", "houx", "hublot", "huit", "huitième", "hurlement",
        "hutte"
    };

    /// <summary>Vowel-initial words that still refuse elision: "le onze", "le oui".</summary>
    private static readonly HashSet<string> NoElision = new(StringComparer.OrdinalIgnoreCase)
    {
        "onze", "onzième", "oui", "ouistiti"
    };

    /// <summary>
    /// The article for a noun, or null when its gender is not known - no article is
    /// better than a wrong one.
    /// </summary>
    public static NounArticle? For(
        string headword,
        GrammaticalGender? gender,
        bool isPluralOnly,
        string? transcription)
    {
        if (gender is null || string.IsNullOrWhiteSpace(headword))
        {
            return null;
        }

        if (isPluralOnly)
        {
            return new NounArticle("les", "des", gender.Value, IsElided: false, IsPlural: true);
        }

        var indefinite = gender switch
        {
            GrammaticalGender.Masculine => "un",
            GrammaticalGender.Feminine => "une",
            _ => "un/une"
        };

        if (Elides(headword, transcription))
        {
            return new NounArticle("l'", indefinite, gender.Value, IsElided: true, IsPlural: false);
        }

        var definite = gender switch
        {
            GrammaticalGender.Masculine => "le",
            GrammaticalGender.Feminine => "la",
            _ => "le/la"
        };

        return new NounArticle(definite, indefinite, gender.Value, IsElided: false, IsPlural: false);
    }

    /// <summary>
    /// Whether "le"/"la" shortens to "l'" before this word: before a vowel sound, which
    /// includes a silent h but not an aspirated one.
    /// </summary>
    public static bool Elides(string headword, string? transcription)
    {
        var word = headword.Trim();
        if (word.Length == 0 || NoElision.Contains(word))
        {
            return false;
        }

        var first = char.ToLowerInvariant(word[0]);

        if (Vowels.Contains(first))
        {
            // The mark is not consulted here: the few vowel-initial words that refuse
            // elision are listed above, and a model-generated transcription with a stray
            // stress mark must not turn "l'arbre" into "le arbre"
            return true;
        }

        if (first == 'h')
        {
            // Aspirated if the transcription says so, or if the word is known to be -
            // the list covers transcriptions from sources that never mark it
            return !HasDisjunctionMark(transcription) && !AspiratedH.Contains(word);
        }

        if (first == 'y')
        {
            // "le yaourt", "le yoga": y before a vowel is a consonant sound
            return word.Length > 1 && !Vowels.Contains(char.ToLowerInvariant(word[1]));
        }

        return false;
    }

    private static bool HasDisjunctionMark(string? transcription)
    {
        var trimmed = transcription?.Trim().TrimStart('[', '/');
        return !string.IsNullOrEmpty(trimmed) && DisjunctionMarks.Contains(trimmed[0]);
    }
}
