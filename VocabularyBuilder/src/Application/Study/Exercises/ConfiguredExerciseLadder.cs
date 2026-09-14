using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// The ladder as an ordered list from configuration. A card carries its own position,
/// which climbs on success and drops on failure, so a word that starts giving trouble
/// meets its earlier, easier exercises again over the following days.
/// </summary>
public class ConfiguredExerciseLadder : IExerciseLadder
{
    private readonly StudyOptions _options;
    private readonly List<LadderRungOptions> _rungs;

    public ConfiguredExerciseLadder(StudyOptions options)
    {
        _options = options;
        _rungs = options.Ladder is { Count: > 0 } ladder ? ladder : new StudyOptions().Ladder;
    }

    public int RungCount => _rungs.Count;

    public ExerciseType TypeAt(int rung) => _rungs[Math.Clamp(rung, 0, _rungs.Count - 1)].Type;

    public int SelectProbeRung(ReviewCard card, string? partOfSpeech, DateTime nowUtc, Func<ExerciseType, bool> canBuild)
    {
        var rung = Math.Clamp(card.CurrentRung, 0, _rungs.Count - 1);

        if (ShouldEscalateForLongGap(card, nowUtc))
        {
            rung = LongGapRung();
        }

        // Step down past rungs this card is not eligible for, then past any whose
        // exercise cannot be built from the material we actually have for the word.
        while (rung > 0 && !(IsEligible(_rungs[rung], card, partOfSpeech) && canBuild(_rungs[rung].Type)))
        {
            rung--;
        }

        return rung;
    }

    public int NextRung(int currentRung, ReviewGrade grade, double recentSuccessRate)
    {
        var rung = Math.Clamp(currentRung, 0, _rungs.Count - 1);

        return grade switch
        {
            ReviewGrade.Again => Math.Max(0, rung - _options.FailureRungDrop),
            ReviewGrade.Hard => rung,
            // The 85% rule: only climb while the card is actually being recalled reliably.
            _ when recentSuccessRate >= _options.TargetSuccessRate => Math.Min(rung + 1, _rungs.Count - 1),
            _ => rung
        };
    }

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

    private int LongGapRung()
    {
        var index = _rungs.FindIndex(r => r.Type == _options.LongGapProbeType);
        return index >= 0 ? index : _rungs.Count - 1;
    }

    private static bool IsEligible(LadderRungOptions rung, ReviewCard card, string? partOfSpeech)
    {
        if (rung.MinIntervalDays is { } minInterval && card.IntervalDays < minInterval)
        {
            return false;
        }

        if (rung.PartsOfSpeech is { Count: > 0 } allowed)
        {
            return partOfSpeech is not null
                && allowed.Any(p => partOfSpeech.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }
}
