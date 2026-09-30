using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// Shared base for the exercises where the word is typed from its meaning.
///
/// Typing is the one form of production that can be marked without asking the learner how
/// it went, which is what lets the top of the ladder - and the criterion for leaving
/// learning - rest on something other than self-report.
/// </summary>
public abstract class TypedExerciseDefinition : ITypedExerciseDefinition
{
    private readonly IGradeResolver _gradeResolver;

    protected TypedExerciseDefinition(IGradeResolver gradeResolver) => _gradeResolver = gradeResolver;

    public abstract ExerciseType Type { get; }

    public GradingMode GradingMode => GradingMode.Automatic;

    public bool CanBeProbe => true;

    public bool CanBuild(StudyMaterial material, DistractorSet? distractors) => material.HasMeaning;

    public ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context) => new()
    {
        Type = Type,
        GradingMode = GradingMode,
        WordId = material.WordId,
        Prompt = material.Meaning!,
        LetterMask = Cue(material, context),
        PartOfSpeech = material.PartOfSpeech
    };

    /// <summary>Part of the spelling to show beside the meaning, or none.</summary>
    protected abstract string? Cue(StudyMaterial material, ExerciseBuildContext context);

    public TypedMatch Match(ExerciseAnswer answer, StudyMaterial material) =>
        answer.Abandoned
            ? new TypedMatch(TypedMatchKind.Wrong, string.Empty)
            : TypedAnswer.Match(answer.Text, material);

    /// <summary>
    /// Only an exact answer is judged on speed. A near miss was known but not cleanly, so it
    /// is Hard: it neither fails the word nor counts towards moving it on.
    /// </summary>
    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material) =>
        Resolve(answer, material, Match(answer, material));

    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material, TypedMatch match)
    {
        return match.Kind switch
        {
            TypedMatchKind.Exact => _gradeResolver.Resolve(
                AnswerKind.Built,
                new AutoGradeSignals(true, answer.ElapsedMs, Length: material.Headword.Length)),
            TypedMatchKind.Wrong => ReviewGrade.Again,
            _ => ReviewGrade.Hard
        };
    }
}

/// <summary>
/// Free production, marked: the meaning is shown and the word is typed with no help at all.
/// </summary>
public class MeaningToWordTypeExerciseDefinition : TypedExerciseDefinition
{
    public MeaningToWordTypeExerciseDefinition(IGradeResolver gradeResolver) : base(gradeResolver) { }

    public override ExerciseType Type => ExerciseType.MeaningToWordType;

    protected override string? Cue(StudyMaterial material, ExerciseBuildContext context) => null;
}

/// <summary>
/// Typing with a head start: the meaning plus the first letters of the word, fewer of them
/// the further the word has got on its level.
///
/// This is diminishing-cues practice, graded. Taking support away while success stays likely
/// is what the evidence favours over repeating the same task at the same difficulty.
/// </summary>
public class MeaningToWordCuedTypeExerciseDefinition : TypedExerciseDefinition
{
    public MeaningToWordCuedTypeExerciseDefinition(IGradeResolver gradeResolver) : base(gradeResolver) { }

    public override ExerciseType Type => ExerciseType.MeaningToWordCuedType;

    protected override string? Cue(StudyMaterial material, ExerciseBuildContext context) =>
        MeaningToWordPartialLettersExerciseDefinition.Mask(
            material.Headword, RevealedLetters(material.Headword, context.CueLevel));

    /// <summary>
    /// About two letters in five for the first three clean answers on the level, then only
    /// the first. On the shipped ladder a word moves up after three, so it types with the
    /// larger cue - the fading there is from syllables to letters to this; a ladder that
    /// keeps words longer fades it further. Always at least one letter hidden, or there
    /// would be nothing left to recall.
    /// </summary>
    public static int RevealedLetters(string headword, int cueLevel)
    {
        var letters = headword.Count(char.IsLetter);

        if (letters <= 3)
        {
            return 1;
        }

        var revealed = cueLevel <= 2 ? (int)Math.Ceiling(letters * 0.4) : 1;
        return Math.Clamp(revealed, 1, letters - 1);
    }
}
