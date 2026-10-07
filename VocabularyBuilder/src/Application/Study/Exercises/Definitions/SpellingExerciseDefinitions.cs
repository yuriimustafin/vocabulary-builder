using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// Shared base for spelling the word while it is, or was a moment ago, in front of the learner.
///
/// Meeting the spelling early, with the hand as well as the eye, is what the first recall of the
/// word has to build on. Marked the way typed answers are (<see cref="TypedAnswer"/>), and not
/// on speed: typing takes the time it takes.
/// </summary>
public abstract class SpellingExerciseDefinition : ITypedExerciseDefinition
{
    public abstract ExerciseType Type { get; }

    public GradingMode GradingMode => GradingMode.Automatic;

    public bool CanBeProbe => true;

    /// <summary>Needs nothing but the word; the meaning is shown beside it when there is one.</summary>
    public bool CanBuild(StudyMaterial material, DistractorSet? distractors) =>
        material.Headword.Any(char.IsLetter);

    public ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context) => new()
    {
        Type = Type,
        GradingMode = GradingMode,
        WordId = material.WordId,
        Prompt = material.Headword,
        Meaning = material.Meaning,
        Tiles = Chunks(material),
        Transcription = material.Transcription,
        PartOfSpeech = material.PartOfSpeech
    };

    /// <summary>The pieces the word is shown and covered in, or none to take it whole.</summary>
    protected virtual IReadOnlyList<string>? Chunks(StudyMaterial material) => null;

    public TypedMatch Match(ExerciseAnswer answer, StudyMaterial material) =>
        answer.Abandoned
            ? new TypedMatch(TypedMatchKind.Wrong, string.Empty)
            : TypedAnswer.Match(Chunks(material) is null ? answer.Text : WithSeparators(answer.Text, material.Headword), material);

    /// <summary>
    /// Typed a chunk at a time, a word's pieces come back without the spaces and hyphens
    /// between them - "audelà" for "au-delà" - so they are put back where the word has them.
    /// </summary>
    private static string? WithSeparators(string? typed, string headword)
    {
        if (typed is null || typed.Any(c => c is ' ' or '-'))
        {
            return typed;
        }

        var result = new System.Text.StringBuilder();
        var next = 0;

        foreach (var c in headword.Trim())
        {
            if (c is ' ' or '-')
            {
                result.Append(c);
            }
            else if (next < typed.Length)
            {
                result.Append(typed[next++]);
            }
        }

        return result.Append(typed[next..]).ToString();
    }

    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material) =>
        Resolve(answer, material, Match(answer, material));

    /// <summary>Exactly right is a success; a slip or a missing accent holds the word; anything else misses.</summary>
    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material, TypedMatch match) => match.Kind switch
    {
        TypedMatchKind.Exact => ReviewGrade.Good,
        TypedMatchKind.Wrong => ReviewGrade.Again,
        _ => ReviewGrade.Hard
    };
}

/// <summary>
/// The word on screen with its meaning, typed out while it stays there. The first thing asked
/// after the introduction: its spelling met, by hand, before it is ever recalled.
/// </summary>
public class WordToSpellingCopyExerciseDefinition : SpellingExerciseDefinition
{
    public override ExerciseType Type => ExerciseType.WordToSpellingCopy;
}

/// <summary>
/// Look, cover, write: the word is shown, then covered, and typed from memory. A word of more
/// than five letters goes a chunk at a time - look at a piece, it is covered, type it, then the
/// next - in the pieces the scramble uses. The ladder marks it mistake-tolerant: a slip here
/// costs the word nothing.
/// </summary>
public class WordToSpellingCoverExerciseDefinition : SpellingExerciseDefinition
{
    /// <summary>Longest word taken in one look.</summary>
    public const int WholeWordLetters = 5;

    public override ExerciseType Type => ExerciseType.WordToSpellingCover;

    protected override IReadOnlyList<string>? Chunks(StudyMaterial material) =>
        material.Headword.Count(char.IsLetter) > WholeWordLetters
            ? Syllabifier.Chunks(material.Headword, material.Language)
            : null;
}
