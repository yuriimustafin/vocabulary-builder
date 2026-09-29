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

    /// <summary>Show the meaning, assemble the word from shuffled letter tiles.</summary>
    MeaningToWordScramble = 4,

    /// <summary>Show the meaning, produce the word unaided, reveal, learner self-grades.</summary>
    MeaningToWordRecall = 5,

    /// <summary>
    /// Follow-up only: meaning plus progressively revealed letters, self-graded.
    /// Never scheduled as a probe - used by the diminishing-cues sequence after a failure.
    /// </summary>
    MeaningToWordPartialLetters = 6,

    /// <summary>
    /// Show the meaning, assemble the word from its shuffled syllables. An easier step
    /// before the letter scramble: the chunks are given, only their order is recalled.
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

    /// <summary>"What can be bright?" - tick the words it goes with among a few it does not.</summary>
    WordToCollocatesChoice = 11,

    /// <summary>Rebuild one of the word's example sentences from its shuffled words, from its translation.</summary>
    TranslationToSentenceScramble = 12,

    /// <summary>
    /// Follow-up only: the word with what ties it to things already known - its mnemonic,
    /// origin and related words - shown after a miss, before it is asked again.
    /// </summary>
    WordToConnectionsReveal = 13
}
