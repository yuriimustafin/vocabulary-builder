using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// Looks up exercise definitions by type. Built from whatever is registered, so a new
/// exercise becomes available to the ladder as soon as its class is added to the container.
/// </summary>
public class ExerciseCatalog : IExerciseCatalog
{
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
