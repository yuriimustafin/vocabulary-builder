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
    /// Follow-up only: meaning plus progressively revealed letters.
    /// Never scheduled as a probe - used by the diminishing-cues sequence after a failure.
    /// </summary>
    MeaningToWordPartialLetters = 6
}
