using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// Looks up exercise definitions by type. Built from whatever is registered, so a new
/// exercise becomes available to the ladder as soon as its class is added to the container.
/// </summary>
public class ExerciseCatalog : IExerciseCatalog
{
    /// <summary>
    /// Exercise types no longer asked, kept in the enum only so the review logs that name them
    /// still read: the syllable scramble, folded into the chunk scramble, and "what goes with it".
    /// </summary>
    public static readonly IReadOnlySet<ExerciseType> Retired = new HashSet<ExerciseType>
    {
        ExerciseType.MeaningToWordSyllableScramble,
        ExerciseType.WordToCollocatesChoice
    };

    private readonly IReadOnlyDictionary<ExerciseType, IExerciseDefinition> _definitions;

    public ExerciseCatalog(IEnumerable<IExerciseDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Type);
    }

    public IExerciseDefinition Get(ExerciseType type) =>
        _definitions.TryGetValue(type, out var definition)
            ? definition
            : throw new InvalidOperationException($"No exercise definition is registered for {type}.");

    public bool CanBuild(ExerciseType type, StudyMaterial material, DistractorSet? distractors) =>
        _definitions.TryGetValue(type, out var definition) && definition.CanBuild(material, distractors);
}
