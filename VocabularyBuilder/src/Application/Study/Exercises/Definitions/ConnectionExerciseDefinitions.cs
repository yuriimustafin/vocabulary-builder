using System.Text.RegularExpressions;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// "What can be bright?" - the word, and a few words to tick the ones it goes with.
///
/// The right ones come from the word's collocates, most typical first, and the wrong ones
/// from words chosen to be clearly impossible beside it: a plausible wrong pairing is one a
/// learner may keep. The first few of each are shown, so marking needs nothing but the word
/// to know which were on screen.
/// </summary>
public class WordToCollocatesChoiceExerciseDefinition : IExerciseDefinition
{
    /// <summary>How many of each are shown.</summary>
    public const int Shown = 3;

    private readonly IGradeResolver _gradeResolver;
    private readonly Random _random;

    public WordToCollocatesChoiceExerciseDefinition(IGradeResolver gradeResolver) : this(gradeResolver, Random.Shared) { }

    /// <summary>Test seam: supply a seeded Random to make the shuffle deterministic.</summary>
    public WordToCollocatesChoiceExerciseDefinition(IGradeResolver gradeResolver, Random random)
    {
        _gradeResolver = gradeResolver;
        _random = random;
    }

    public ExerciseType Type => ExerciseType.WordToCollocatesChoice;

    public GradingMode GradingMode => GradingMode.Automatic;

    public bool CanBeProbe => true;

    /// <summary>
    /// Two of each at least, or there is no choice to make - and never for a word that does
    /// not combine with a range of partners, whatever the model offered for it.
    /// </summary>
    public bool CanBuild(StudyMaterial material, DistractorSet? distractors) =>
        CombinesWithPartners(material.PartOfSpeech)
        && material.Collocates.Count >= 2
        && material.NonCollocates.Count >= 2;

    /// <summary>
    /// Parts of speech that have no partners to speak of, as each source spells them - the
    /// model ("interjection"), WordReference ("interj", "prép") and Oxford ("exclamation").
    /// "What goes with bonjour?" has no honest answer, so the prompt asks for none and this
    /// holds even when an answer arrives anyway.
    /// </summary>
    private static readonly string[] WithoutPartners =
    {
        "interj", "exclam", "greeting", "prep", "prép", "conj", "pron", "art", "det", "dét", "num"
    };

    public static bool CombinesWithPartners(string? partOfSpeech)
    {
        var kind = partOfSpeech?.Trim().ToLowerInvariant();

        return string.IsNullOrEmpty(kind) || !WithoutPartners.Any(prefix => kind.StartsWith(prefix, StringComparison.Ordinal));
    }

    public ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context) => new()
    {
        Type = Type,
        GradingMode = GradingMode,
        WordId = material.WordId,
        Prompt = material.Headword,
        Options = Right(material).Concat(Wrong(material)).OrderBy(_ => _random.Next()).ToList(),
        Transcription = material.Transcription,
        PartOfSpeech = material.PartOfSpeech
    };

    /// <summary>
    /// Every one of them right is judged on speed like any recognition; one slip - a word
    /// missed or a wrong one ticked - is Hard; more is a miss.
    /// </summary>
    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material)
    {
        if (answer.Abandoned)
        {
            return ReviewGrade.Again;
        }

        var errors = Errors(answer.Selections ?? Array.Empty<string>(), material);

        return errors switch
        {
            0 => _gradeResolver.Resolve(Type, new AutoGradeSignals(true, answer.ElapsedMs)),
            1 => ReviewGrade.Hard,
            _ => ReviewGrade.Again
        };
    }

    /// <summary>The ones that should have been ticked.</summary>
    public static IReadOnlyList<string> Right(StudyMaterial material) => material.Collocates.Take(Shown).ToList();

    private static IEnumerable<string> Wrong(StudyMaterial material) => material.NonCollocates.Take(Shown);

    /// <summary>Right ones left unticked, and anything ticked that is not one of them.</summary>
    private static int Errors(IReadOnlyList<string> selected, StudyMaterial material)
    {
        var right = Right(material).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ticked = selected.Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return right.Count(r => !ticked.Contains(r)) + ticked.Count(t => !right.Contains(t));
    }
}

/// <summary>
/// One of the word's example sentences, its words shuffled, to be put back in order from
/// what it says.
///
/// Rebuilding the sentence puts the word back among the words it is used with, which is
/// what the examples were chosen to show. An ordering slip says more about the sentence than
/// about the word, so the ladder marks this one mistake-tolerant.
/// </summary>
public class TranslationToSentenceScrambleExerciseDefinition : IExerciseDefinition
{
    private const int MinWords = 3;
    private const int MaxWords = 14;
    private static readonly char[] Punctuation = ",.;:!?¡¿«»\"“”()".ToCharArray();

    private readonly IGradeResolver _gradeResolver;
    private readonly Random _random;

    public TranslationToSentenceScrambleExerciseDefinition(IGradeResolver gradeResolver) : this(gradeResolver, Random.Shared) { }

    /// <summary>Test seam: supply a seeded Random to make the shuffle deterministic.</summary>
    public TranslationToSentenceScrambleExerciseDefinition(IGradeResolver gradeResolver, Random random)
    {
        _gradeResolver = gradeResolver;
        _random = random;
    }

    public ExerciseType Type => ExerciseType.TranslationToSentenceScramble;

    public GradingMode GradingMode => GradingMode.Automatic;

    public bool CanBeProbe => true;

    /// <summary>Only a stored example, so rebuilding it counts as practising that sentence.</summary>
    public bool CanBuild(StudyMaterial material, DistractorSet? distractors) =>
        material.HasStoredExample
        && material.HasContextSentence
        && Words(material.ContextSentence!).Count is >= MinWords and <= MaxWords;

    public ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context)
    {
        var words = Words(material.ContextSentence!);
        var tiles = words.OrderBy(_ => _random.Next()).ToList();

        for (var attempt = 0; attempt < 10 && tiles.SequenceEqual(words); attempt++)
        {
            tiles = words.OrderBy(_ => _random.Next()).ToList();
        }

        return new ExercisePayload
        {
            Type = Type,
            GradingMode = GradingMode,
            WordId = material.WordId,
            // What the sentence says, or failing that what the word means
            Prompt = material.ContextSentenceTranslation ?? material.Meaning ?? material.Headword,
            Tiles = tiles,
            ExampleId = material.ExampleId,
            PartOfSpeech = material.PartOfSpeech
        };
    }

    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material)
    {
        var correct = !answer.Abandoned
            && answer.Text is not null
            && material.ContextSentence is not null
            && Normalise(answer.Text) == Normalise(material.ContextSentence);

        return _gradeResolver.Resolve(
            Type,
            new AutoGradeSignals(correct, answer.ElapsedMs, answer.Resets, answer.Abandoned,
                material.ContextSentence?.Count(char.IsLetter) ?? 0));
    }

    /// <summary>
    /// The sentence's words, stripped of the punctuation around them and with the first one
    /// lower-cased - a capital would say which tile goes first.
    /// </summary>
    public static List<string> Words(string sentence)
    {
        var words = sentence
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim(Punctuation))
            .Where(w => w.Length > 0)
            .ToList();

        if (words.Count > 0 && words[0].Length > 1 && !words[0].Skip(1).All(char.IsUpper))
        {
            words[0] = char.ToLowerInvariant(words[0][0]) + words[0][1..];
        }

        return words;
    }

    private static string Normalise(string value) =>
        Regex.Replace(string.Join(' ', Words(value)).ToLowerInvariant(), @"\s+", " ").Trim();
}

/// <summary>
/// Follow-up only: the word with its mnemonic, where it comes from and what it is related
/// to, shown after a miss and before it is asked again. Support at the moment it is needed,
/// rather than a thing to be tested on.
/// </summary>
public class WordToConnectionsRevealExerciseDefinition : SelfGradedExerciseDefinition
{
    public override ExerciseType Type => ExerciseType.WordToConnectionsReveal;

    public override bool CanBeProbe => false;

    public override bool CanBuild(StudyMaterial material, DistractorSet? distractors) =>
        material.Mnemonic is not null || material.Etymology is not null || material.Cognates is not null;

    public override ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context) => new()
    {
        Type = Type,
        GradingMode = GradingMode,
        WordId = material.WordId,
        Prompt = material.Headword,
        Answer = material.Meaning,
        Transcription = material.Transcription,
        PartOfSpeech = material.PartOfSpeech,
        Connections = material.Connections
    };
}
