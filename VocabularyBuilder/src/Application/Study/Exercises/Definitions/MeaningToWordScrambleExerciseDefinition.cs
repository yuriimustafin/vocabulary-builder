using System.Globalization;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// Spelling from tiles: the meaning is shown and the word has to be rebuilt from its
/// shuffled letters.
///
/// Every letter is on the table, which makes this a strongly cued task - easier than
/// producing the word unaided - so it sits below free recall on the ladder. Decoy letters
/// can be mixed in to take some of that support away.
/// </summary>
public class MeaningToWordScrambleExerciseDefinition : IExerciseDefinition
{
    private const string DecoyAlphabet = "abcdefghijklmnopqrstuvwxyz";

    private readonly StudyOptions _options;
    private readonly IGradeResolver _gradeResolver;
    private readonly Random _random;

    public MeaningToWordScrambleExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver)
        : this(options, gradeResolver, Random.Shared)
    {
    }

    /// <summary>Test seam: supply a seeded Random to make the shuffle deterministic.</summary>
    public MeaningToWordScrambleExerciseDefinition(StudyOptions options, IGradeResolver gradeResolver, Random random)
    {
        _options = options;
        _gradeResolver = gradeResolver;
        _random = random;
    }

    public ExerciseType Type => ExerciseType.MeaningToWordScramble;

    public GradingMode GradingMode => GradingMode.Automatic;

    public bool CanBeProbe => true;

    /// <summary>A one-letter word has nothing to rearrange.</summary>
    public bool CanBuild(StudyMaterial material, DistractorSet? distractors) =>
        material.HasMeaning && Letters(material.Headword).Count > 1;

    public ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context)
    {
        var tiles = Letters(material.Headword)
            .Concat(Decoys(material.Headword))
            .OrderBy(_ => _random.Next())
            .ToList();

        return new ExercisePayload
        {
            Type = Type,
            GradingMode = GradingMode,
            WordId = material.WordId,
            Prompt = material.Meaning!,
            Tiles = tiles,
            PartOfSpeech = material.PartOfSpeech,
            ContextSentence = material.ContextSentence
        };
    }

    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material)
    {
        // Spaces and hyphens in a multi-word headword are not part of what is being tested.
        var correct = answer.Text is not null
            && string.Equals(Normalise(answer.Text), Normalise(material.Headword), StringComparison.OrdinalIgnoreCase);

        return _gradeResolver.Resolve(
            Type,
            new AutoGradeSignals(correct, answer.ElapsedMs, answer.Resets, answer.Abandoned));
    }

    /// <summary>
    /// Split by text element rather than by char so that accented letters stay on one tile
    /// instead of breaking into a base letter and a combining mark.
    /// </summary>
    private static List<string> Letters(string headword)
    {
        var letters = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(headword.Trim());

        while (enumerator.MoveNext())
        {
            var element = (string)enumerator.Current;
            if (!string.IsNullOrWhiteSpace(element) && element != "-")
            {
                letters.Add(element);
            }
        }

        return letters;
    }

    private IEnumerable<string> Decoys(string headword)
    {
        if (_options.ScrambleDecoyLetters <= 0)
        {
            yield break;
        }

        // Letters the word does not contain, so a decoy can never be mistaken for a
        // legitimate tile the learner simply has not placed yet.
        var used = Letters(headword).Select(l => l.ToLowerInvariant()).ToHashSet();
        var available = DecoyAlphabet.Select(c => c.ToString()).Where(c => !used.Contains(c)).ToList();

        for (var i = 0; i < _options.ScrambleDecoyLetters && available.Count > 0; i++)
        {
            var index = _random.Next(available.Count);
            yield return available[index];
            available.RemoveAt(index);
        }
    }

    private static string Normalise(string value) =>
        new(value.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
}
