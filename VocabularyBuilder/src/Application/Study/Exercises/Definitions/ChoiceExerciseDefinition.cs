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

        return _gradeResolver.Resolve(
            Type,
            new AutoGradeSignals(correct, answer.ElapsedMs, answer.Resets, answer.Abandoned));
    }

    /// <summary>Showing the pronunciation would give away a word the learner is meant to pick.</summary>
    protected virtual bool ShowTranscription => true;

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

    protected override string? Target(StudyMaterial material) => material.Meaning;

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

    protected override string? Stimulus(StudyMaterial material) => material.Meaning;

    protected override string? Target(StudyMaterial material) => material.Headword;

    protected override IReadOnlyList<string> WrongOptions(DistractorSet distractors) => distractors.Headwords;

    // The transcription belongs to the answer here, so it cannot be shown with the prompt.
    protected override bool ShowTranscription => false;
}
