using VocabularyBuilder.Domain.Enums;

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

    /// <summary>Choices for a multiple-choice exercise, already shuffled.</summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>Letter tiles to assemble, already shuffled.</summary>
    public IReadOnlyList<string>? Tiles { get; init; }

    /// <summary>Partially revealed spelling, for example "d _ _ _ _ _".</summary>
    public string? LetterMask { get; init; }

    /// <summary>Extra context shown alongside the answer once revealed.</summary>
    public string? ContextSentence { get; init; }
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
public record ExerciseBuildContext(
    DistractorSet? Distractors = null,
    int RevealedLetters = 0,
    bool AllowHint = true);

/// <summary>
/// What the learner did.
/// </summary>
/// <param name="Text">Chosen option, or the word as assembled.</param>
/// <param name="SelfGrade">The learner's own judgement, for self-graded exercises.</param>
public record ExerciseAnswer(
    string? Text = null,
    ReviewGrade? SelfGrade = null,
    int ElapsedMs = 0,
    int Resets = 0,
    bool HintUsed = false,
    bool Abandoned = false);

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
}

public interface IExerciseCatalog
{
    IExerciseDefinition Get(ExerciseType type);

    bool CanBuild(ExerciseType type, StudyMaterial material, DistractorSet? distractors);
}
