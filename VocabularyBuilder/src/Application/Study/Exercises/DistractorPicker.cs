namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// A word from the learner's own collection that could stand in as a wrong answer.
/// </summary>
public record DistractorCandidate(int WordId, string Headword, string? PartOfSpeech, int? Frequency, string? Meaning);

/// <summary>
/// The wrong answers for one multiple-choice exercise, already sized to the option count.
/// </summary>
public record DistractorSet(IReadOnlyList<string> Meanings, IReadOnlyList<string> Headwords);

public interface IDistractorPicker
{
    /// <summary>
    /// Chooses wrong answers for a word from the surrounding corpus. Returns null when
    /// there are not enough usable candidates, which makes the ladder fall back a rung
    /// rather than showing a question that gives itself away.
    /// </summary>
    DistractorSet? Pick(StudyMaterial target, IReadOnlyList<DistractorCandidate> pool);
}

/// <summary>
/// Draws distractors from words the learner already has rather than inventing them.
/// Real vocabulary makes better wrong answers - it is genuinely confusable - and it
/// costs nothing to produce.
///
/// Candidates of the same part of speech come first, and among those the ones closest
/// in frequency, so a rare word is not obviously distinguishable from three common ones.
/// A shuffle over a wider band than is needed stops the same trio recurring every time
/// a word comes up.
/// </summary>
public class DistractorPicker : IDistractorPicker
{
    private const int SelectionBandMultiplier = 3;

    private readonly StudyOptions _options;
    private readonly Random _random;

    public DistractorPicker(StudyOptions options) : this(options, Random.Shared)
    {
    }

    /// <summary>Test seam: supply a seeded Random to make selection deterministic.</summary>
    public DistractorPicker(StudyOptions options, Random random)
    {
        _options = options;
        _random = random;
    }

    public DistractorSet? Pick(StudyMaterial target, IReadOnlyList<DistractorCandidate> pool)
    {
        var needed = Math.Max(1, _options.ChoiceOptionCount - 1);

        var usable = pool
            .Where(c => c.WordId != target.WordId)
            .Where(c => !string.IsNullOrWhiteSpace(c.Headword))
            .Where(c => !Matches(c.Headword, target.Headword))
            .ToList();

        var headwords = Choose(usable, target, needed, c => c.Headword);

        var meaningCandidates = usable
            .Where(c => !string.IsNullOrWhiteSpace(c.Meaning))
            .Where(c => !Matches(c.Meaning!, target.Meaning))
            .ToList();

        var meanings = Choose(meaningCandidates, target, needed, c => c.Meaning!);

        // Both directions have to be satisfiable, so the caller can build either
        // multiple-choice rung from one set.
        if (headwords.Count < needed || meanings.Count < needed)
        {
            return null;
        }

        return new DistractorSet(meanings, headwords);
    }

    private List<string> Choose(
        IReadOnlyList<DistractorCandidate> candidates,
        StudyMaterial target,
        int needed,
        Func<DistractorCandidate, string> select)
    {
        var samePartOfSpeech = candidates.Where(c => SharesPartOfSpeech(c, target)).ToList();
        var rest = candidates.Except(samePartOfSpeech).ToList();

        var chosen = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tier in new[] { samePartOfSpeech, rest })
        {
            foreach (var value in NearestByFrequency(tier, target, needed).Select(select))
            {
                if (chosen.Count == needed)
                {
                    return chosen;
                }

                if (seen.Add(value))
                {
                    chosen.Add(value);
                }
            }
        }

        return chosen;
    }

    /// <summary>
    /// Narrows to the candidates nearest the target in frequency, then shuffles that band
    /// so the exercise varies between sittings without drifting into unfair comparisons.
    /// </summary>
    private IEnumerable<DistractorCandidate> NearestByFrequency(
        IReadOnlyList<DistractorCandidate> candidates,
        StudyMaterial target,
        int needed)
    {
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var targetFrequency = FrequencyOf(target, candidates);

        return candidates
            .OrderBy(c => c.Frequency is null ? int.MaxValue : Math.Abs(c.Frequency.Value - targetFrequency))
            .Take(Math.Max(needed, needed * SelectionBandMultiplier))
            .OrderBy(_ => _random.Next());
    }

    /// <summary>
    /// The target's own frequency is not carried on the material, so the band is centred on
    /// the median of the pool. That still keeps very rare and very common words apart.
    /// </summary>
    private static int FrequencyOf(StudyMaterial target, IReadOnlyList<DistractorCandidate> candidates)
    {
        var known = candidates.Where(c => c.Frequency.HasValue).Select(c => c.Frequency!.Value).OrderBy(f => f).ToList();
        return known.Count == 0 ? 0 : known[known.Count / 2];
    }

    private static bool SharesPartOfSpeech(DistractorCandidate candidate, StudyMaterial target)
    {
        if (string.IsNullOrWhiteSpace(candidate.PartOfSpeech) || string.IsNullOrWhiteSpace(target.PartOfSpeech))
        {
            return false;
        }

        return candidate.PartOfSpeech.Contains(target.PartOfSpeech, StringComparison.OrdinalIgnoreCase)
            || target.PartOfSpeech.Contains(candidate.PartOfSpeech, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(string value, string? other) =>
        other is not null && string.Equals(value.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase);
}
