using System.Text.RegularExpressions;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// Everything an exercise can draw on for one word, already resolved from whichever
/// source had it. Exercises never touch the entities, so they neither know nor care
/// whether a definition came from a dictionary or had to be generated.
/// </summary>
public record StudyMaterial
{
    public required int WordId { get; init; }

    public required string Headword { get; init; }

    public Language Language { get; init; }

    public string? PartOfSpeech { get; init; }

    public string? Transcription { get; init; }

    /// <summary>
    /// The article the word is learned with, when it is a French noun of known gender.
    /// Kept apart from <see cref="Headword"/>, which answers are marked against.
    /// </summary>
    public NounArticleDto? Article { get; init; }

    /// <summary>Short definition used as the stimulus or the answer, depending on direction.</summary>
    public string? Meaning { get; init; }

    /// <summary>
    /// Which sense <see cref="Meaning"/> is, said in the language being learned. Shown
    /// beside the meaning where a whole card is on screen, and never used as a stimulus or
    /// an answer - it names a sense rather than giving it.
    /// </summary>
    public string? MeaningGloss { get; init; }

    /// <summary>
    /// An example sentence that is guaranteed to contain <see cref="ContextForm"/> - the
    /// resolver discards any that do not, because a cloze exercise has nothing to blank out.
    /// </summary>
    public string? ContextSentence { get; init; }

    /// <summary>
    /// The word as it appears in <see cref="ContextSentence"/>: the headword, or the form
    /// the example was written with - "prend" in a sentence for "prendre".
    /// </summary>
    public string? ContextForm { get; init; }

    /// <summary>The stored example the sentence came from, so answering it can be recorded against it.</summary>
    public int? ExampleId { get; init; }

    /// <summary>What the word is typically said of or used with.</summary>
    public string? Usage { get; init; }

    public string? Etymology { get; init; }

    /// <summary>Words in another language sharing its origin, with any false friend flagged.</summary>
    public string? Cognates { get; init; }

    /// <summary>Sound-alike words and a scene tying them to the meaning.</summary>
    public string? Mnemonic { get; init; }

    /// <summary>What the word is typically used with, most typical first.</summary>
    public IReadOnlyList<string> Collocates { get; init; } = Array.Empty<string>();

    /// <summary>What it clearly does not go with.</summary>
    public IReadOnlyList<string> NonCollocates { get; init; } = Array.Empty<string>();

    /// <summary>
    /// What each collocate and non-collocate means, in the learner's language, for those the
    /// model translated. Keyed without regard to case.
    /// </summary>
    public IReadOnlyDictionary<string, string> PhraseTranslations { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <see cref="ContextSentence"/> is one of the word's stored examples, practice on it tracked.</summary>
    public bool HasStoredExample => ExampleId is not null;

    /// <summary>
    /// What <see cref="ContextSentence"/> says, in the learner's language. Kept apart from
    /// the sentence so a cloze blanks the sentence alone.
    /// </summary>
    public string? ContextSentenceTranslation { get; init; }

    public bool HasMeaning => !string.IsNullOrWhiteSpace(Meaning);

    /// <summary>
    /// The meaning is the word itself, as a translation sometimes is - "poison" for le poison,
    /// "information" for l'information. Asking for the word from that meaning, or for that
    /// meaning from the word, shows the answer in the question.
    /// </summary>
    public bool MeaningGivesAwayWord =>
        HasMeaning && Meaning!.Split(',', ';', '/').Select(Bare).Any(gloss => gloss.Length > 0 && gloss == Bare(Headword));

    /// <summary>Whether an exercise can ask from the meaning, or for it, without giving the word away.</summary>
    public bool CanAskFromMeaning => HasMeaning && !MeaningGivesAwayWord;

    /// <summary>A meaning or headword as it would be compared: no case, accents, notes in brackets or leading article.</summary>
    private static string Bare(string text) =>
        Regex.Replace(
            TypedAnswer.StripAccents(TypedAnswer.Tidy(Regex.Replace(text, @"\([^)]*\)", " "))),
            @"^(?:a|an|the|to)\s+", string.Empty);

    public bool HasContextSentence => !string.IsNullOrWhiteSpace(ContextSentence);

    /// <summary><see cref="ContextSentence"/> with the word cut out of it.</summary>
    public string BlankedContextSentence => HeadwordText.Blankify(ContextSentence!, ContextForm ?? Headword);

    /// <summary>Everything that ties the word to something already known, for the card to show.</summary>
    public WordConnectionsDto? Connections => WordConnectionsDto.From(Usage, Etymology, Cognates, Mnemonic);
}

/// <summary>What ties a word to things the learner already knows.</summary>
public record WordConnectionsDto(string? Usage, string? Etymology, string? Cognates, string? Mnemonic)
{
    /// <summary>None at all when there is nothing to show, so the card can leave the section out.</summary>
    public static WordConnectionsDto? From(string? usage, string? etymology, string? cognates, string? mnemonic) =>
        usage is null && etymology is null && cognates is null && mnemonic is null
            ? null
            : new WordConnectionsDto(usage, etymology, cognates, mnemonic);
}

/// <summary>
/// What a word is missing. Only these get sent to generation; anything the dictionary
/// already supplied is left alone.
/// </summary>
[Flags]
public enum StudyMaterialGaps
{
    None = 0,
    Meaning = 1,
    ContextSentence = 2,

    /// <summary>Fewer example sentences than a word is given to practise on.</summary>
    Examples = 4,

    /// <summary>A form the word was met in that no example sentence uses yet.</summary>
    Forms = 8,

    /// <summary>Usage, etymology, cognates and a mnemonic, never generated for this word.</summary>
    Connections = 16
}

/// <summary>
/// Locating and blanking a headword inside a sentence. Whole-word, case-insensitive,
/// and Unicode-aware so accented French headwords match.
/// </summary>
public static class HeadwordText
{
    public const string Blank = "_____";

    public static bool Contains(string? sentence, string headword) =>
        sentence is not null && Pattern(headword).IsMatch(sentence);

    /// <summary>Replaces the first whole-word occurrence of the headword with a blank.</summary>
    public static string Blankify(string sentence, string headword) =>
        Pattern(headword).Replace(sentence, Blank, 1);

    private static Regex Pattern(string headword) =>
        new($@"\b{Regex.Escape(headword.Trim())}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
