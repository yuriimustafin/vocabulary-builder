using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// The word put back together from its pieces: the meaning is shown and the word has to be
/// rebuilt from three or four shuffled chunks - its syllables wherever they make that many
/// (<see cref="Syllabifier.Chunks"/>), single letters only for a word of three or fewer.
///
/// It used to be a word's worth of single letters, with a separate syllable version beside it.
/// Ordering seven letters is a search, not a recall: a real session missed or slowed on half
/// of them. A handful of chunks asks for the shape of the word without the search.
/// </summary>
public class MeaningToWordScrambleExerciseDefinition : IExerciseDefinition
{
    private readonly IGradeResolver _gradeResolver;
    private readonly Random _random;

    public MeaningToWordScrambleExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver)
        : this(options, gradeResolver, Random.Shared)
    {
    }

    /// <summary>Test seam: supply a seeded Random to make the shuffle deterministic.</summary>
    public MeaningToWordScrambleExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver, Random random)
    {
        _gradeResolver = gradeResolver;
        _random = random;
    }

    public ExerciseType Type => ExerciseType.MeaningToWordScramble;

    public GradingMode GradingMode => GradingMode.Automatic;

    public bool CanBeProbe => true;

    /// <summary>A one-letter word has nothing to rearrange, and a meaning that is the word itself spells it out.</summary>
    public bool CanBuild(StudyMaterial material, DistractorSet? distractors) =>
        material.CanAskFromMeaning && Pieces(material).Count >= 2;

    public ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context)
    {
        var pieces = Pieces(material);

        return new ExercisePayload
        {
            Type = Type,
            GradingMode = GradingMode,
            WordId = material.WordId,
            Prompt = material.Meaning!,
            Tiles = Shuffle(pieces),
            PartOfSpeech = material.PartOfSpeech,
            ContextSentence = material.ContextSentence,
            ContextSentenceTranslation = material.ContextSentenceTranslation,
            MeaningGloss = material.MeaningGloss
        };
    }

    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material)
    {
        // Spaces and hyphens in a multi-word headword are not part of what is being tested.
        var correct = answer.Text is not null
            && string.Equals(Normalise(answer.Text), Normalise(material.Headword), StringComparison.OrdinalIgnoreCase);

        return _gradeResolver.Resolve(
            AnswerKind.Built,
            new AutoGradeSignals(correct, answer.ElapsedMs, answer.Resets, answer.Abandoned, material.Headword.Length));
    }

    private static IReadOnlyList<string> Pieces(StudyMaterial material) =>
        Syllabifier.Chunks(material.Headword, material.Language);

    /// <summary>
    /// Shuffled, and never left in the word's own order when there is another: tiles that
    /// already spell the word would ask nothing.
    /// </summary>
    private List<string> Shuffle(IReadOnlyList<string> inOrder)
    {
        var shuffled = inOrder.OrderBy(_ => _random.Next()).ToList();

        for (var attempt = 0; attempt < 10 && shuffled.SequenceEqual(inOrder); attempt++)
        {
            shuffled = inOrder.OrderBy(_ => _random.Next()).ToList();
        }

        if (shuffled.SequenceEqual(inOrder) && shuffled.Count > 1)
        {
            // Every piece the same, near enough: move the first to the end.
            shuffled.Add(shuffled[0]);
            shuffled.RemoveAt(0);
        }

        return shuffled;
    }

    private static string Normalise(string value) =>
        new(value.Normalize(System.Text.NormalizationForm.FormC).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
}
