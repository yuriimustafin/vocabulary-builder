using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// The ladder as levels from configuration, each with a pool of exercises.
///
/// A word does not climb one exercise per correct answer - that shows a word each exercise
/// exactly once and spends most of its practice on the easiest ones. It stays on a level
/// until it has a run of clean successes there, meeting progressively harder exercises from
/// the level's pool as the run grows, and drops back one level on a miss.
/// </summary>
public class ConfiguredExerciseLadder : IExerciseLadder
{
    private readonly StudyOptions _options;
    private readonly IReadOnlyList<LadderRungOptions> _rungs;

    public ConfiguredExerciseLadder(StudyOptions options)
    {
        _options = options;
        _rungs = options.EffectiveLadder;
    }

    public int RungCount => _rungs.Count;

    public int TopRung => _rungs.Count - 1;

    public ExerciseType TypeAt(int rung) => TypesAt(rung)[0];

    public IReadOnlyList<ExerciseType> TypesAt(int rung) =>
        _rungs[Clamp(rung)].Exercises.Select(e => e.Type).ToList();

    public ProbeChoice SelectProbe(
        ReviewCard card, string? partOfSpeech, DateTime nowUtc, Func<ExerciseType, bool> canBuild)
    {
        var rung = Clamp(card.CurrentRung);

        if (ShouldEscalateForLongGap(card, nowUtc) && canBuild(_options.LongGapProbeType))
        {
            return new ProbeChoice(RungOf(_options.LongGapProbeType), _options.LongGapProbeType, Escalated: true, 0);
        }

        // Step down past levels with nothing this word can be asked, so a word with no
        // sentence or no distractors meets the nearest level that works rather than none.
        for (; rung >= 0; rung--)
        {
            var available = _rungs[rung].Exercises
                .Where(e => IsEligible(e, card, partOfSpeech) && canBuild(e.Type))
                .Select(e => e.Type)
                .ToList();

            if (available.Count > 0)
            {
                // A word that fell back to a lower level is starting afresh there.
                var cueLevel = rung == card.CurrentRung ? card.RungStreak : 0;
                return new ProbeChoice(rung, Pick(available, cueLevel, card.LastExerciseType), Escalated: false, cueLevel);
            }
        }

        return new ProbeChoice(0, TypeAt(0), Escalated: false, 0);
    }

    public RungMove NextRung(ReviewCard card, ReviewGrade grade, bool hintUsed)
    {
        var rung = Clamp(card.CurrentRung);

        if (grade == ReviewGrade.Again)
        {
            return new RungMove(Math.Max(0, rung - _options.FailureRungDrop), 0);
        }

        // Correct, but only just, or only with the cue: the word holds its place, neither
        // counting towards moving up nor being sent down.
        if (grade == ReviewGrade.Hard || hintUsed)
        {
            return new RungMove(rung, card.RungStreak);
        }

        var streak = card.RungStreak + 1;
        var promoteAfter = _rungs[rung].PromoteAfter;

        return promoteAfter > 0 && streak >= promoteAfter && rung < TopRung
            ? new RungMove(rung + 1, 0)
            : new RungMove(rung, streak);
    }

    /// <summary>
    /// The exercise at the word's point in the pool, unless that is the one it was just
    /// asked, when the next one along is used instead - doing the same thing twice running
    /// is the least useful repetition there is.
    ///
    /// On a level a word moves up from, the streak never outruns the pool, so this walks it
    /// easiest to hardest and the support fades. On the top level the streak keeps growing
    /// and the pool is taken in turn.
    /// </summary>
    private static ExerciseType Pick(IReadOnlyList<ExerciseType> available, int streak, ExerciseType? last)
    {
        var index = streak % available.Count;

        return available[index] == last && available.Count > 1
            ? available[(index + 1) % available.Count]
            : available[index];
    }

    private int RungOf(ExerciseType type)
    {
        var index = _rungs.ToList().FindIndex(r => r.Exercises.Any(e => e.Type == type));
        return index >= 0 ? index : TopRung;
    }

    private int Clamp(int rung) => Math.Clamp(rung, 0, _rungs.Count - 1);

    /// <summary>
    /// After a long absence the probe jumps to unhinted production. That is both the
    /// cleanest measurement - no cue to inflate the grade - and the largest learning gain.
    /// </summary>
    private bool ShouldEscalateForLongGap(ReviewCard card, DateTime nowUtc)
    {
        if (card.CurrentRung < _options.LongGapEscalationMinRung || card.LastReviewedAtUtc is null)
        {
            return false;
        }

        var gapDays = (nowUtc - card.LastReviewedAtUtc.Value).TotalDays;
        if (gapDays <= 0)
        {
            return false;
        }

        return gapDays >= _options.LongGapDays
            || gapDays / Math.Max(1, card.IntervalDays) >= _options.OverdueRatio;
    }

    private static bool IsEligible(LadderExerciseOptions exercise, ReviewCard card, string? partOfSpeech)
    {
        if (exercise.MinIntervalDays is { } minInterval && card.IntervalDays < minInterval)
        {
            return false;
        }

        if (exercise.PartsOfSpeech is { Count: > 0 } allowed)
        {
            return partOfSpeech is not null
                && allowed.Any(p => partOfSpeech.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }
}
