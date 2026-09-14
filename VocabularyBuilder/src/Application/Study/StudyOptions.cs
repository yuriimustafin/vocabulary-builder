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

    /// <summary>Hour (UTC) at which "today" rolls over, so a late-night session counts as one day.</summary>
    public int DayRolloverHourUtc { get; set; } = 4;

    // --- SM-2 --------------------------------------------------------------

    /// <summary>
    /// Same-day steps before a card graduates. Two steps means three touches on day one,
    /// which is what puts the first three exercise types in the first session.
    /// </summary>
    public int[] LearningStepsMinutes { get; set; } = { 1, 10 };

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
    /// </summary>
    public List<LadderRungOptions> Ladder { get; set; } = new()
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
