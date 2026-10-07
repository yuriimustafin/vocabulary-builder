using System.Text.RegularExpressions;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises.Definitions;

/// <summary>
/// One of the word's example sentences, given as written but for a gap: the word and two or
/// three words around it - the phrase it is used in, where the example names one - shuffled
/// as three tiles to put back in order. A tile may hold two short words ("par la").
///
/// It used to be the whole sentence in pieces, prompted by its translation: a sentence-ordering
/// puzzle that half the time was missed, and took sixteen seconds when it was not. What it is
/// for is the word among the words it is used with, so only that much is asked.
///
/// The translation of the sentence and of each tile is a free hint. An ordering slip says more
/// about the sentence than about the word, so the ladder marks this mistake-tolerant, and keeps
/// it for reviews (<c>MinIntervalDays</c>) rather than the first day.
/// </summary>
public class TranslationToSentenceScrambleExerciseDefinition : IExerciseDefinition
{
    private const int MaxWords = 20;
    private const int Tiles = 3;
    private static readonly char[] Punctuation = ",.;:!?¡¿«»\"“”()…".ToCharArray();

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

    /// <summary>The translations only make the sentence readable; putting it together is still the learner's.</summary>
    public bool HintIsFree => true;

    /// <summary>
    /// Only a stored example, so rebuilding it counts as practising that sentence; only one
    /// with a translation, for the hint; and only one long enough that some of it is left
    /// standing around the gap.
    /// </summary>
    public bool CanBuild(StudyMaterial material, DistractorSet? distractors) =>
        material.HasStoredExample
        && material.HasContextSentence
        && !string.IsNullOrWhiteSpace(material.ContextSentenceTranslation)
        && Gap(material) is not null;

    public ExercisePayload Build(StudyMaterial material, ExerciseBuildContext context)
    {
        var gap = Gap(material)!;
        var tiles = gap.Tiles.OrderBy(_ => _random.Next()).ToList();

        for (var attempt = 0; attempt < 10 && tiles.SequenceEqual(gap.Tiles); attempt++)
        {
            tiles = gap.Tiles.OrderBy(_ => _random.Next()).ToList();
        }

        if (tiles.SequenceEqual(gap.Tiles) && tiles.Count > 1)
        {
            tiles.Add(tiles[0]);
            tiles.RemoveAt(0);
        }

        var glosses = gap.Tiles
            .Select(tile => (Tile: tile, Gloss: Gloss(tile, material.ContextGlosses)))
            .Where(t => t.Gloss is not null)
            .DistinctBy(t => t.Tile)
            .ToDictionary(t => t.Tile, t => t.Gloss!);

        return new ExercisePayload
        {
            Type = Type,
            GradingMode = GradingMode,
            WordId = material.WordId,
            Prompt = gap.Start + string.Join(" ", Enumerable.Repeat(HeadwordText.Blank, gap.Tiles.Count)) + gap.End,
            SentenceStart = gap.Start,
            SentenceEnd = gap.End,
            Tiles = tiles,
            ExampleId = material.ExampleId,
            PartOfSpeech = material.PartOfSpeech,
            // The free hint: what the sentence says, and what each tile means
            ContextSentenceTranslation = material.ContextSentenceTranslation,
            OptionHints = glosses.Count > 0 ? glosses : null
        };
    }

    public ReviewGrade Resolve(ExerciseAnswer answer, StudyMaterial material)
    {
        var gap = Gap(material);

        var correct = !answer.Abandoned
            && answer.Text is not null
            && gap is not null
            && Normalise(answer.Text) == Normalise(string.Join(' ', gap.Tiles));

        // Reading the free hint takes time; a right answer after it is not marked down as slow
        if (correct && answer.FreeHintTaken)
        {
            return ReviewGrade.Good;
        }

        return _gradeResolver.Resolve(
            AnswerKind.Built,
            new AutoGradeSignals(correct, answer.ElapsedMs, answer.Resets, answer.Abandoned,
                gap?.Tiles.Sum(t => t.Count(char.IsLetter)) ?? 0));
    }

    /// <summary>
    /// The sentence split around its gap: the text before it, the tiles in their right order,
    /// and the text after. The gap is the word - the form the sentence uses - and its
    /// neighbours up to four words in a sentence of seven or more, three in a shorter one,
    /// taking first the words of the phrase the example was built around.
    /// </summary>
    public static SentenceGap? Gap(StudyMaterial material)
    {
        var sentence = material.ContextSentence;

        if (string.IsNullOrWhiteSpace(sentence))
        {
            return null;
        }

        var tokens = Regex.Matches(sentence, @"\S+").ToList();
        var found = HeadwordText.Locate(sentence, material.ContextForm ?? material.Headword);

        if (found is not { } at || tokens.Count > MaxWords)
        {
            return null;
        }

        var inGap = Enumerable.Range(0, tokens.Count)
            .Where(i => tokens[i].Index < at.Index + at.Length && tokens[i].Index + tokens[i].Length > at.Index)
            .ToList();

        var size = Math.Max(tokens.Count >= 7 ? 4 : 3, inGap.Count);

        // Some of the sentence has to be left standing, or this is the old puzzle again
        if (inGap.Count == 0 || tokens.Count <= size)
        {
            return null;
        }

        var related = (material.ContextCollocation ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Bare)
            .ToHashSet();

        int first = inGap[0], last = inGap[^1];

        while (last - first + 1 < size)
        {
            var canLeft = first > 0;
            var canRight = last < tokens.Count - 1;
            var leftRelated = canLeft && related.Contains(Bare(tokens[first - 1].Value));
            var rightRelated = canRight && related.Contains(Bare(tokens[last + 1].Value));

            if (rightRelated || (canRight && !leftRelated))
            {
                last++;
            }
            else if (canLeft)
            {
                first--;
            }
            else
            {
                break;
            }
        }

        var words = tokens.Skip(first).Take(last - first + 1).Select(t => t.Value).ToList();

        // Punctuation at the edges of the gap stays with the sentence, so no tile shows where it goes
        var leading = words[0][..^words[0].TrimStart(Punctuation).Length];
        var trailing = words[^1][words[^1].TrimEnd(Punctuation).Length..];
        words[0] = words[0][leading.Length..];
        words[^1] = words[^1][..^trailing.Length];

        if (words.Any(w => w.Length == 0))
        {
            return null;
        }

        var start = sentence[..tokens[first].Index] + leading;
        var end = trailing + sentence[(tokens[last].Index + tokens[last].Length)..];

        return new SentenceGap(start, TilesOf(words), end);
    }

    /// <summary>
    /// Three tiles from the gap's words: a short word - an article, a preposition - shares a
    /// tile with the word after it until there are only three.
    /// </summary>
    private static List<string> TilesOf(List<string> words)
    {
        var tiles = words.ToList();

        while (tiles.Count > Tiles)
        {
            var join = Enumerable.Range(0, tiles.Count - 1)
                .OrderBy(i => tiles[i].Count(char.IsLetter) <= 3 ? 0 : 1)
                .ThenBy(i => tiles[i].Length + tiles[i + 1].Length)
                .ThenBy(i => i)
                .First();

            tiles[join] = tiles[join] + " " + tiles[join + 1];
            tiles.RemoveAt(join + 1);
        }

        return tiles;
    }

    /// <summary>What a tile says, from the sentence's word-by-word glosses; null when none of it is glossed.</summary>
    private static string? Gloss(string tile, IReadOnlyDictionary<string, string> glosses)
    {
        var parts = tile.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => glosses.TryGetValue(Bare(word), out var meaning) ? meaning : null)
            .Where(meaning => meaning is not null)
            .ToList();

        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }

    /// <summary>A word as glosses and answers are compared: lower case, no punctuation round it.</summary>
    public static string Bare(string word) => word.Trim(Punctuation).ToLowerInvariant();

    private static string Normalise(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Bare).Where(w => w.Length > 0));
}

/// <summary>A sentence with a gap to rebuild: the text either side, and the tiles in their right order.</summary>
public record SentenceGap(string Start, IReadOnlyList<string> Tiles, string End);

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
