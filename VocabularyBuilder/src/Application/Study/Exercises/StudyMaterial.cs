using System.Text.RegularExpressions;
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

    /// <summary>Short definition used as the stimulus or the answer, depending on direction.</summary>
    public string? Meaning { get; init; }

    /// <summary>
    /// An example sentence that is guaranteed to contain the headword - the resolver
    /// discards any that do not, because a cloze exercise has nothing to blank out.
    /// </summary>
    public string? ContextSentence { get; init; }

    public bool HasMeaning => !string.IsNullOrWhiteSpace(Meaning);

    public bool HasContextSentence => !string.IsNullOrWhiteSpace(ContextSentence);
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
    ContextSentence = 2
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
