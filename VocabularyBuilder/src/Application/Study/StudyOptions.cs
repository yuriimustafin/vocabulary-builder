using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study;

/// <summary>
/// Everything tunable about the study loop. Bound from the "Study" section of appsettings
/// so exercise types, their order and the pace can be changed without touching code.
/// </summary>
public class StudyOptions
{
    public const string SectionName = "Study";

    // --- session shape -----------------------------------------------------

    /// <summary>Cap on words introduced per day, split evenly across the three selection buckets.</summary>
    public int NewCardsPerDay { get; set; } = 12;

    public int MaxCardsPerSession { get; set; } = 60;

    /// <summary>
    /// New words introduced per batch. Small on purpose: the session refetches when a batch
    /// runs out, so a few at a time is what lets the first tests fall due and mix in among
    /// the next introductions instead of arriving as one block afterwards.
    /// </summary>
    public int NewCardsPerBatch { get; set; } = 4;

    /// <summary>
    /// How early a learning step may be served so that it mixes in among the words still
    /// being introduced.
    ///
    /// The first step is a minute out, and a batch only ever holds a few new words, so
    /// without a little grace the first tests are never quite due when the next batch is
    /// fetched - and a session becomes every new word first, every test afterwards. Kept
    /// short on purpose: it should catch the one-minute step, not collapse the ten-minute
    /// one.
    /// </summary>
    public int InterleaveWindowMinutes { get; set; } = 2;

    /// <summary>
    /// How far ahead a learning step may be pulled forward when nothing else is left.
    ///
    /// Without this the first day stops dead: twelve words introduced leaves twelve cards
    /// due a minute from now and nothing due this instant. Rather than end the session,
    /// a step within this window is brought forward - the cost being that a step set for
    /// ten minutes may be seen after four.
    /// </summary>
    public int LearnAheadMinutes { get; set; } = 20;

    /// <summary>Hour (UTC) at which "today" rolls over, so a late-night session counts as one day.</summary>
    public int DayRolloverHourUtc { get; set; } = 4;

    // --- SM-2 --------------------------------------------------------------

    /// <summary>
    /// Same-day steps before a card graduates. Two steps means three touches on day one,
    /// which is what puts the first three exercise types in the first session.
    ///
    /// Left empty on purpose: configuration binding appends to a collection that already
    /// has contents rather than replacing it, so a populated default here would turn the
    /// configured [1, 10] into [1, 10, 1, 10] and the card would never graduate. Read it
    /// through <see cref="EffectiveLearningSteps"/>, which supplies the default.
    /// </summary>
    public int[] LearningStepsMinutes { get; set; } = Array.Empty<int>();

    /// <summary>The configured steps, or the built-in default when none are configured.</summary>
    public int[] EffectiveLearningSteps =>
        LearningStepsMinutes.Length > 0 ? LearningStepsMinutes : StudyDefaults.LearningStepsMinutes;

    public int GraduatingIntervalDays { get; set; } = 1;
    public int EasyIntervalDays { get; set; } = 4;
    public double MinEaseFactor { get; set; } = 1.3;
    public double EasyBonus { get; set; } = 1.3;
    public double HardIntervalMultiplier { get; set; } = 1.2;

    /// <summary>Percentage of the pre-lapse interval kept after a failure. 0 restarts from one day.</summary>
    public int LapseIntervalPercent { get; set; }

    /// <summary>Random spread applied to day-scale intervals so same-day cohorts do not clump forever.</summary>
    public int IntervalFuzzPercent { get; set; } = 5;

    public int MaxIntervalDays { get; set; } = 365;

    /// <summary>Interval at which a word counts as known rather than still bedding in.</summary>
    public int MatureIntervalDays { get; set; } = 21;

    // --- probe selection ---------------------------------------------------

    /// <summary>Absolute gap after which the probe escalates to unhinted production.</summary>
    public int LongGapDays { get; set; } = 7;

    /// <summary>Gap as a multiple of the scheduled interval that also triggers escalation.</summary>
    public double OverdueRatio { get; set; } = 1.5;

    /// <summary>Lowest rung that long-gap escalation applies to; below this the word is still being learned.</summary>
    public int LongGapEscalationMinRung { get; set; } = 3;

    /// <summary>Probe used after a long gap. Falls back to the last rung when absent from the ladder.</summary>
    public ExerciseType LongGapProbeType { get; set; } = ExerciseType.MeaningToWordRecall;

    /// <summary>Success rate a card must hold to climb a rung - the "85% rule".</summary>
    public double TargetSuccessRate { get; set; } = 0.85;

    /// <summary>Weight of the newest review in the success-rate moving average.</summary>
    public double SuccessEmaAlpha { get; set; } = 0.3;

    /// <summary>Rungs a card falls when a probe is failed, so earlier exercises come back.</summary>
    public int FailureRungDrop { get; set; } = 2;

    // --- automatic grading -------------------------------------------------

    /// <summary>A correct answer at or under this is graded Easy.</summary>
    public int FastAnswerMs { get; set; } = 3000;

    /// <summary>A correct answer at or over this is graded Hard.</summary>
    public int SlowAnswerMs { get; set; } = 10000;

    // --- difficulty --------------------------------------------------------

    public DifficultyTierOptions DifficultyTiers { get; set; } = new();

    public FollowUpCountOptions FollowUpsByTier { get; set; } = new();

    // --- enrichment --------------------------------------------------------

    /// <summary>
    /// How long a generation claim is trusted. A claim older than this is assumed to have
    /// been abandoned by a crash or restart and the word is picked up again.
    /// </summary>
    public int EnrichmentStaleClaimMinutes { get; set; } = 5;

    /// <summary>Failures allowed before a word is left alone rather than retried forever.</summary>
    public int EnrichmentMaxAttempts { get; set; } = 3;

    /// <summary>Words the distractor pool is drawn from, per session.</summary>
    public int DistractorPoolSize { get; set; } = 500;

    // --- exercise content --------------------------------------------------

    /// <summary>
    /// Options shown by a multiple-choice rung, the correct one included. A rung cannot be
    /// built unless one fewer usable distractor than this is available, so a thin corpus
    /// makes the ladder fall back rather than showing a give-away two-option question.
    /// </summary>
    public int ChoiceOptionCount { get; set; } = 4;

    /// <summary>Extra letters mixed into the scramble tiles that do not belong to the word.</summary>
    public int ScrambleDecoyLetters { get; set; }

    // --- the ladder --------------------------------------------------------

    /// <summary>
    /// Ordered easiest to hardest. A card's CurrentRung indexes this list, so reordering
    /// or inserting a rung is a configuration change.
    ///
    /// Empty by default for the same reason as the learning steps: a populated default
    /// would be appended to, not replaced, leaving a ladder with every rung twice. Read it
    /// through <see cref="EffectiveLadder"/>.
    /// </summary>
    public List<LadderRungOptions> Ladder { get; set; } = new();

    /// <summary>The configured ladder, or the built-in default when none is configured.</summary>
    public IReadOnlyList<LadderRungOptions> EffectiveLadder =>
        Ladder.Count > 0 ? Ladder : StudyDefaults.Ladder;
}

/// <summary>
/// The built-in defaults, kept out of the bound properties so configuration replaces them
/// instead of being appended to them.
/// </summary>
public static class StudyDefaults
{
    public static int[] LearningStepsMinutes => new[] { 1, 10 };

    public static IReadOnlyList<LadderRungOptions> Ladder => new List<LadderRungOptions>
    {
        new() { Type = ExerciseType.WordToMeaningReveal },
        new() { Type = ExerciseType.WordToMeaningChoice },
        new() { Type = ExerciseType.MeaningToWordChoice },
        new() { Type = ExerciseType.ContextToWordRecall },
        new() { Type = ExerciseType.MeaningToWordScramble },
        new() { Type = ExerciseType.MeaningToWordRecall }
    };
}

public class LadderRungOptions
{
    public ExerciseType Type { get; set; }

    /// <summary>Withhold this rung until the card's interval reaches this many days.</summary>
    public int? MinIntervalDays { get; set; }

    /// <summary>Restrict this rung to words whose part of speech contains one of these, case-insensitively.</summary>
    public List<string>? PartsOfSpeech { get; set; }
}

public class DifficultyTierOptions
{
    public double ComfortableMinEase { get; set; } = 2.3;
    public double ComfortableMinSuccess { get; set; } = 0.85;
    public double DifficultMaxEase { get; set; } = 1.8;
    public int DifficultMinLapses { get; set; } = 2;
    public double DifficultMaxSuccess { get; set; } = 0.70;
}

/// <summary>
/// How many ungraded re-encoding exercises follow the graded probe, by difficulty tier.
/// </summary>
public class FollowUpCountOptions
{
    public int Comfortable { get; set; }
    public int Shaky { get; set; } = 1;
    public int Difficult { get; set; } = 2;

    public int For(CardDifficulty difficulty) => difficulty switch
    {
        CardDifficulty.Comfortable => Comfortable,
        CardDifficulty.Shaky => Shaky,
        CardDifficulty.Difficult => Difficult,
        _ => 0
    };
}
