namespace VocabularyBuilder.Domain.Enums;

/// <summary>
/// An exercise presented during a study session.
/// Named as &lt;Stimulus&gt;To&lt;Target&gt;&lt;Mode&gt; so future types stay unambiguous
/// (e.g. AudioToWordType, WordToAudioReveal, ContextToMeaningChoice).
/// </summary>
public enum ExerciseType
{
    /// <summary>Flashcard: show the word, reveal the meaning, learner self-grades.</summary>
    WordToMeaningReveal = 0,

    /// <summary>Show the word, pick the correct meaning from four options.</summary>
    WordToMeaningChoice = 1,

    /// <summary>Show the meaning, pick the correct word from four options.</summary>
    MeaningToWordChoice = 2,

    /// <summary>Cloze: sentence with the word blanked out; the meaning sits behind a Hint button.</summary>
    ContextToWordRecall = 3,

    /// <summary>
    /// Show the meaning, put the word's pieces in order: three or four chunks, syllables
    /// where they make that many - single letters only for a word of three or fewer.
    /// </summary>
    MeaningToWordScramble = 4,

    /// <summary>Show the meaning, produce the word unaided, reveal, learner self-grades.</summary>
    MeaningToWordRecall = 5,

    /// <summary>
    /// Follow-up only: meaning plus progressively revealed letters, self-graded.
    /// Never scheduled as a probe - used by the diminishing-cues sequence after a failure.
    /// </summary>
    MeaningToWordPartialLetters = 6,

    /// <summary>
    /// Retired: the word from its shuffled syllables. <see cref="MeaningToWordScramble"/>
    /// shuffles chunks of the word now - syllables where they make three or four - and
    /// took its place. Kept so the review logs that name it still read.
    /// </summary>
    MeaningToWordSyllableScramble = 7,

    /// <summary>Show the meaning, type the word. Marked automatically, forgiving accents and one slip.</summary>
    MeaningToWordType = 8,

    /// <summary>
    /// Show the meaning and part of the spelling, type the whole word. Fewer letters are
    /// shown the further the word has got on its level.
    /// </summary>
    MeaningToWordCuedType = 9,

    /// <summary>A sentence with the word blanked out; pick the word that fills it from four.</summary>
    ContextToWordChoice = 10,

    /// <summary>
    /// Retired: "what can be bright?" - tick the words it goes with. Dropped: a real model's
    /// wrong options were often not wrong, and a third of the answers missed. Kept so the
    /// review logs that name it still read.
    /// </summary>
    WordToCollocatesChoice = 11,

    /// <summary>
    /// One of the word's example sentences, given as written but for a gap: the word and two
    /// or three words around it, to be put back in order from a few shuffled tiles.
    /// </summary>
    TranslationToSentenceScramble = 12,

    /// <summary>
    /// Follow-up only: the word with what ties it to things already known - its mnemonic,
    /// origin and related words - shown after a miss, before it is asked again.
    /// </summary>
    WordToConnectionsReveal = 13,

    /// <summary>
    /// The word on screen with its meaning, typed out while it stays there: its spelling met
    /// before it is ever recalled. The first thing asked after the introduction.
    /// </summary>
    WordToSpellingCopy = 14,

    /// <summary>
    /// Look, cover, write: the word is shown, covered, and typed from memory - a long one a
    /// chunk at a time. A slip costs nothing.
    /// </summary>
    WordToSpellingCover = 15
}
