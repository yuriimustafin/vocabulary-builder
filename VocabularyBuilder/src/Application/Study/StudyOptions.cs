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
    /// Minutes until the next try while a word is being learned: the first entry after the
    /// introduction or a miss, then one step further along per success. The last entry
    /// repeats. The steps only set the pace - a word leaves learning when it meets its exit
    /// criterion (<see cref="LearningExitSuccesses"/>), not when the steps run out.
    ///
    /// Left empty on purpose: configuration binding appends to a collection that already
    /// has contents rather than replacing it, so a populated default here would turn the
    /// configured [1, 3, 5, 8] into [1, 3, 5, 8, 1, 3, 5, 8]. Read it through
    /// <see cref="EffectiveLearningSteps"/>, which supplies the default.
    /// </summary>
    public int[] LearningStepsMinutes { get; set; } = Array.Empty<int>();

    /// <summary>The configured steps, or the built-in default when none are configured.</summary>
    public int[] EffectiveLearningSteps =>
        LearningStepsMinutes.Length > 0 ? LearningStepsMinutes : StudyDefaults.LearningStepsMinutes;

    /// <summary>
    /// Clean successes on the top level a new word needs before it leaves learning. Two, a
    /// few minutes apart, is the "recall to criterion" the first session is for: the word
    /// has been produced unaided twice, not just recognised.
    /// </summary>
    public int LearningExitSuccesses { get; set; } = 2;

    /// <summary>The same for a word relearning after a lapse. It was known once, so one is enough.</summary>
    public int RelearningExitSuccesses { get; set; } = 1;

    /// <summary>
    /// Minimum gap between the last two of those successes. Two correct answers a few
    /// seconds apart show the word is still in working memory, not that it has been learned.
    /// </summary>
    public int LearningExitSpacingMinutes { get; set; } = 4;

    /// <summary>
    /// Graded tries after which a word leaves learning on its next success regardless, so a
    /// stubborn word cannot take over the session. It comes back tomorrow either way.
    /// </summary>
    public int MaxLearningRetrievals { get; set; } = 10;

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

    /// <summary>Lowest level that long-gap escalation applies to; below this the word is still being learned.</summary>
    public int LongGapEscalationMinRung { get; set; } = 3;

    /// <summary>Probe used after a long gap. Falls back to the top level's first exercise when absent from the ladder.</summary>
    public ExerciseType LongGapProbeType { get; set; } = ExerciseType.MeaningToWordType;

    /// <summary>Weight of the newest review in the success-rate moving average.</summary>
    public double SuccessEmaAlpha { get; set; } = 0.3;

    /// <summary>
    /// Levels a word falls when a probe is failed. One: the word meets the level below -
    /// where it was last succeeding - rather than being sent back to the start.
    /// </summary>
    public int FailureRungDrop { get; set; } = 1;

    // --- automatic grading -------------------------------------------------

    /// <summary>
    /// A correct answer at or under this is graded Easy - except multiple choice, which is
    /// capped at Good because recognising a word is not the same as producing it.
    /// </summary>
    public int FastAnswerMs { get; set; } = 3000;

    /// <summary>A correct answer at or over this is graded Hard.</summary>
    public int SlowAnswerMs { get; set; } = 10000;

    /// <summary>
    /// Allowance per letter where the learner builds the word piece by piece. A long word
    /// takes longer to assemble however well it is known, so the slow threshold for those
    /// exercises is the larger of <see cref="SlowAnswerMs"/> and this times its length.
    /// </summary>
    public int SlowAnswerMsPerLetter { get; set; } = 1500;

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
    /// Levels, easiest to hardest. A card's CurrentRung indexes this list. Each level has a
    /// pool of exercises, easiest first: a word on that level is asked the exercise at its
    /// streak, so the support fades as it succeeds, and never the same one twice running
    /// when the pool has another.
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
    public static int[] LearningStepsMinutes => new[] { 1, 3, 5, 8 };

    public static IReadOnlyList<LadderRungOptions> Ladder => new List<LadderRungOptions>
    {
        // Met, not graded, the first time; a word dropped this far is graded here once.
        Level("Introduction", 1, ExerciseType.WordToMeaningReveal),

        // One success: recognising a word again adds little once it is recognised. The
        // other two are what a word dropped here meets, so it is not the question it missed.
        Level("Recognition", 1,
            ExerciseType.MeaningToWordChoice,
            ExerciseType.ContextToWordChoice,
            ExerciseType.WordToMeaningChoice),

        // Three, so the support can fade: the chunks of the word, then its letters, then
        // typing it from its first letters.
        Level("Scaffolded", 3,
            ExerciseType.MeaningToWordSyllableScramble,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.MeaningToWordCuedType),

        // Where a word stays, taking these in turn.
        Level("Production", 0,
            ExerciseType.ContextToWordRecall,
            ExerciseType.MeaningToWordType,
            ExerciseType.MeaningToWordRecall)
    };

    private static LadderRungOptions Level(string name, int promoteAfter, params ExerciseType[] types) => new()
    {
        Name = name,
        PromoteAfter = promoteAfter,
        Exercises = types.Select(type => new LadderExerciseOptions { Type = type }).ToList()
    };
}

/// <summary>One level of the ladder.</summary>
public class LadderRungOptions
{
    /// <summary>For people reading the configuration; nothing depends on it.</summary>
    public string? Name { get; set; }

    /// <summary>The exercises this level can ask, easiest first.</summary>
    public List<LadderExerciseOptions> Exercises { get; set; } = new();

    /// <summary>
    /// Clean successes in a row that move a word up to the next level. Zero means never:
    /// the top level is where a word stays, and the introduction is left by being met.
    /// </summary>
    public int PromoteAfter { get; set; }
}

public class LadderExerciseOptions
{
    public ExerciseType Type { get; set; }

    /// <summary>Withhold this exercise until the card's interval reaches this many days.</summary>
    public int? MinIntervalDays { get; set; }

    /// <summary>Restrict this exercise to words whose part of speech contains one of these, case-insensitively.</summary>
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
