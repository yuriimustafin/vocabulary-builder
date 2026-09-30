using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// Shared base for the two multiple-choice rungs. They differ only in which way round
/// the word and its meaning are put, so everything else - option shuffling, marking,
/// the minimum number of distractors - lives here.
/// </summary>
public abstract class ChoiceExerciseDefinition : IExerciseDefinition
{
    private readonly StudyOptions _options;
    private readonly IGradeResolver _gradeResolver;
    private readonly Random _random;

    protected ChoiceExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver, Random random)
    {
        _options = options;
        _gradeResolver = gradeResolver;
        _random = random;
    }

    public abstract ExerciseType Type { get; }

    public GradingMode GradingMode => GradingMode.Automatic;

    public bool CanBeProbe => true;

    public bool AsksToRecognise => true;

    public virtual bool HintIsFree => false;

    /// <summary>The text the learner is shown.</summary>
    protected abstract string? Stimulus(StudyMaterial material);

    /// <summary>The option that is correct.</summary>
    protected abstract string? Target(StudyMaterial material);

    /// <summary>The wrong options to mix in.</summary>
    protected abstract IReadOnlyList<string> WrongOptions(DistractorSet distractors);

    public bool CanBuild(StudyMaterial material, DistractorSet? distractors)
    {
        if (distractors is null
            || string.IsNullOrWhiteSpace(Stimulus(material))
            || string.IsNullOrWhiteSpace(Target(material)))
        {
            return false;
        }

        return WrongOptions(distractors).Count >= RequiredDistractors;
    }

    public ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context)
    {
        if (context.Distractors is null)
        {
            throw new InvalidOperationException($"{Type} needs distractors; CanBuild should have been checked first.");
        }

        var options = WrongOptions(context.Distractors)
            .Take(RequiredDistractors)
            .Append(Target(material)!)
            .OrderBy(_ => _random.Next())
            .ToList();

        return new ExercisePayload
        {
            Type = Type,
            GradingMode = GradingMode,
            WordId = material.WordId,
            Prompt = Stimulus(material)!,
            ExampleId = ExampleId(material),
            Hint = Hint(material),
            Options = options,
            Transcription = ShowTranscription ? material.Transcription : null,
            PartOfSpeech = material.PartOfSpeech
        };
    }

    /// <summary>
    /// Marked against the word rather than against a remembered option index, which is
    /// what lets the payload go to the browser without carrying the answer.
    /// </summary>
    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material)
    {
        var correct = answer.Text is not null
            && string.Equals(answer.Text.Trim(), Target(material)?.Trim(), StringComparison.OrdinalIgnoreCase);

        // Reading a free hint takes time; a right answer after one is not marked down as slow
        if (correct && answer.FreeHintTaken && !answer.Abandoned)
        {
            return ReviewGrade.Good;
        }

        return _gradeResolver.Resolve(
            AnswerKind.Recognised,
            new AutoGradeSignals(correct, answer.ElapsedMs, answer.Resets, answer.Abandoned));
    }

    /// <summary>Showing the pronunciation would give away a word the learner is meant to pick.</summary>
    protected virtual bool ShowTranscription => true;

    /// <summary>The stored example the question is asked from, for the one that asks from a sentence.</summary>
    protected virtual int? ExampleId(StudyMaterial material) => null;

    /// <summary>What the learner can ask to see, or nothing when the exercise offers no hint.</summary>
    protected virtual string? Hint(StudyMaterial material) => null;

    private int RequiredDistractors => Math.Max(1, _options.ChoiceOptionCount - 1);
}

/// <summary>
/// Recognition: given the word, choose what it means.
/// </summary>
public class WordToMeaningChoiceExerciseDefinition : ChoiceExerciseDefinition
{
    public WordToMeaningChoiceExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver)
        : this(options, gradeResolver, Random.Shared) { }

    public WordToMeaningChoiceExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver, Random random)
        : base(options, gradeResolver, random) { }

    public override ExerciseType Type => ExerciseType.WordToMeaningChoice;

    protected override string? Stimulus(StudyMaterial material) => material.Headword;

    // Not offered when the meaning is the word itself: the right option would repeat the prompt
    protected override string? Target(StudyMaterial material) => material.CanAskFromMeaning ? material.Meaning : null;

    protected override IReadOnlyList<string> WrongOptions(DistractorSet distractors) => distractors.Meanings;
}

/// <summary>
/// The reverse direction: given the meaning, choose the word. Harder than recognition,
/// because the meaning cues a set of candidates rather than one form.
/// </summary>
public class MeaningToWordChoiceExerciseDefinition : ChoiceExerciseDefinition
{
    public MeaningToWordChoiceExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver)
        : this(options, gradeResolver, Random.Shared) { }

    public MeaningToWordChoiceExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver, Random random)
        : base(options, gradeResolver, random) { }

    public override ExerciseType Type => ExerciseType.MeaningToWordChoice;

    // Not offered when the meaning is the word itself: the prompt would be the answer
    protected override string? Stimulus(StudyMaterial material) => material.CanAskFromMeaning ? material.Meaning : null;

    protected override string? Target(StudyMaterial material) => material.Headword;

    protected override IReadOnlyList<string> WrongOptions(DistractorSet distractors) => distractors.Headwords;

    // The transcription belongs to the answer here, so it cannot be shown with the prompt.
    protected override bool ShowTranscription => false;
}

/// <summary>
/// A sentence with the word cut out, and four words to fill it from.
///
/// Recognition still - the word is on screen - but the learner has to judge which one fits
/// the context, which asks more of them than matching a definition. It is also what a word
/// dropped back to recognition meets, so it is not asked the question it has just missed.
/// </summary>
public class ContextToWordChoiceExerciseDefinition : ChoiceExerciseDefinition
{
    public ContextToWordChoiceExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver)
        : this(options, gradeResolver, Random.Shared) { }

    public ContextToWordChoiceExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver, Random random)
        : base(options, gradeResolver, random) { }

    public override ExerciseType Type => ExerciseType.ContextToWordChoice;

    protected override string? Stimulus(StudyMaterial material) =>
        material.HasContextSentence ? material.BlankedContextSentence : null;

    protected override int? ExampleId(StudyMaterial material) => material.ExampleId;

    protected override string? Target(StudyMaterial material) => material.Headword;

    protected override IReadOnlyList<string> WrongOptions(DistractorSet distractors) => distractors.Headwords;

    // The pronunciation is of the missing word.
    protected override bool ShowTranscription => false;

    /// <summary>
    /// What the missing word means. A generic sentence - "Il a perdu son _____." - fits any
    /// noun among the options, and a real session missed a quarter of these for that reason
    /// rather than for not knowing the word. The meaning settles which one is meant, so asking
    /// for it costs nothing. None when the meaning is the word itself: that would be the answer.
    /// </summary>
    protected override string? Hint(StudyMaterial material) => material.CanAskFromMeaning ? material.Meaning : null;

    public override bool HintIsFree => true;
}
