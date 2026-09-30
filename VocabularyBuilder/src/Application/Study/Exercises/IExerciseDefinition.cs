using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Application.Common.Models;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// One rendered exercise, as sent to the client.
///
/// Nothing here identifies the correct choice. Automatically graded exercises are marked
/// against the word itself when the answer comes back, so the payload can be handed to
/// the browser without giving the answer away.
/// </summary>
public record ExercisePayload
{
    public required ExerciseType Type { get; init; }

    public required GradingMode GradingMode { get; init; }

    public required int WordId { get; init; }

    /// <summary>The stimulus: a headword, a meaning, or a sentence with a gap in it.</summary>
    public required string Prompt { get; init; }

    /// <summary>Shown once the learner asks to see it. Only ever set for self-graded exercises.</summary>
    public string? Answer { get; init; }

    /// <summary>Optional support, hidden until requested. Never offered on an unhinted probe.</summary>
    public string? Hint { get; init; }

    public bool HintAvailable => Hint is not null;

    public string? Transcription { get; init; }

    public string? PartOfSpeech { get; init; }

    /// <summary>
    /// Article for the word being studied, shown beside it wherever the word itself is
    /// on screen. Never part of an answer - the headword alone is what gets marked.
    /// </summary>
    public NounArticleDto? Article { get; init; }

    /// <summary>Choices for a multiple-choice exercise, already shuffled.</summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>Letter tiles to assemble, already shuffled.</summary>
    public IReadOnlyList<string>? Tiles { get; init; }

    /// <summary>Partially revealed spelling, for example "d _ _ _ _ _".</summary>
    public string? LetterMask { get; init; }

    /// <summary>Extra context shown alongside the answer once revealed.</summary>
    public string? ContextSentence { get; init; }

    /// <summary>
    /// Which sense the meaning is, in the language being learned. Carried so the card can
    /// show it on its own line; it is never a prompt, an answer or a choice.
    /// </summary>
    public string? MeaningGloss { get; init; }

    /// <summary>What <see cref="ContextSentence"/> says, in the learner's language.</summary>
    public string? ContextSentenceTranslation { get; init; }

    /// <summary>
    /// The stored example the exercise is built on, sent back with the answer so that
    /// answering it is recorded against that sentence. Set only by exercises that ask
    /// about a sentence.
    /// </summary>
    public int? ExampleId { get; init; }

    /// <summary>What ties the word to things already known: usage, origin, related words, a mnemonic.</summary>
    public WordConnectionsDto? Connections { get; init; }

    /// <summary>
    /// For an exercise with several options, what each means - keyed by the option - for the
    /// learner to ask to see. Only the options the model translated are in it.
    /// </summary>
    public Dictionary<string, string>? OptionHints { get; init; }
}

/// <summary>
/// What the caller wants from this particular rendering.
/// </summary>
/// <param name="Distractors">Wrong answers, for multiple-choice exercises.</param>
/// <param name="RevealedLetters">Letters to expose, for the diminishing-cues follow-up.</param>
/// <param name="AllowHint">
/// False on a probe that has been escalated after a long absence: a cue there would
/// inflate the grade and stretch the next interval on evidence that was never earned.
/// </param>
/// <param name="CueLevel">
/// How far the support has faded for this word on its level - see <see cref="ProbeChoice.CueLevel"/>.
/// </param>
public record ExerciseBuildContext(
    DistractorSet? Distractors = null,
    int RevealedLetters = 0,
    bool AllowHint = true,
    int CueLevel = 0);

/// <summary>
/// What the learner did.
/// </summary>
/// <param name="Text">Chosen option, or the word as assembled.</param>
/// <param name="SelfGrade">The learner's own judgement, for self-graded exercises.</param>
/// <param name="Selections">Every option ticked, for an exercise with more than one right answer.</param>
/// <param name="FreeHintTaken">
/// A hint the exercise offers for free was opened. It costs nothing - including the seconds
/// spent reading it, which would otherwise mark a right answer down as slow.
/// </param>
public record ExerciseAnswer(
    string? Text = null,
    ReviewGrade? SelfGrade = null,
    int ElapsedMs = 0,
    int Resets = 0,
    bool HintUsed = false,
    bool Abandoned = false,
    IReadOnlyList<string>? Selections = null,
    bool FreeHintTaken = false);

/// <summary>
/// One exercise type. Adding a seventh kind of question means writing one of these,
/// registering it, and adding the matching component on the client - nothing else in
/// the scheduling or session machinery needs to know about it.
/// </summary>
public interface IExerciseDefinition
{
    ExerciseType Type { get; }

    GradingMode GradingMode { get; }

    /// <summary>
    /// False for exercises that exist only as re-encoding support and are never graded,
    /// so the ladder never selects them as the measured attempt.
    /// </summary>
    bool CanBeProbe { get; }

    /// <summary>Whether this exercise can actually be rendered from the material available.</summary>
    bool CanBuild(StudyMaterial material, DistractorSet? distractors);

    ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context);

    /// <summary>
    /// Grades the answer. Self-graded exercises pass the learner's own judgement through;
    /// automatic ones mark the answer against the word.
    /// </summary>
    ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material);

    /// <summary>
    /// For an exercise with several right answers, what they were, for the feedback to list.
    /// Null for one with a single answer.
    /// </summary>
    IReadOnlyList<string>? ExpectedOptions(StudyMaterial material) => null;

    /// <summary>
    /// The word is picked out among options rather than produced. A miss there is not
    /// knowing which word it was, so what follows is the word's connections, not its letters.
    /// </summary>
    bool AsksToRecognise => false;

    /// <summary>
    /// The hint only makes a choice among real words fair - a translation - so taking it
    /// neither holds the word on its level nor marks the answer down.
    /// </summary>
    bool HintIsFree => false;
}

/// <summary>
/// An exercise where the word is typed. Marked leniently, so the handler asks for the match
/// itself as well as the grade: to say what was nearly right, and to catch a "typo" that is
/// really another word.
/// </summary>
public interface ITypedExerciseDefinition : IExerciseDefinition
{
    TypedMatch Match(ExerciseAnswer answer, StudyMaterial material);

    /// <summary>Grades an answer already matched, so the match is made once.</summary>
    ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material, TypedMatch match);
}

public interface IExerciseCatalog
{
    IExerciseDefinition Get(ExerciseType type);

    bool CanBuild(ExerciseType type, StudyMaterial material, DistractorSet? distractors);
}
