using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class ScaffoldSequencerTests
{
    private static ScaffoldSequencer Sequencer(StudyOptions? options = null)
    {
        var opts = options ?? new StudyOptions();
        return new ScaffoldSequencer(opts, new ConfiguredExerciseLadder(opts));
    }

    [Test]
    public void AWordRecalledCleanlyGetsNoFollowUps()
    {
        Sequencer().Build(3, ReviewGrade.Good, CardDifficulty.Comfortable, headwordLength: 8)
            .Should().BeEmpty();
    }

    [Test]
    public void AMissPickingTheWordAmongOptionsBringsOnlyItsConnections()
    {
        // Not knowing which word it was: walking it through its letters answers another question
        Sequencer().Build(1, ReviewGrade.Again, CardDifficulty.Shaky, headwordLength: 8, recognition: true)
            .Select(s => s.Type).Should().Equal(ExerciseType.WordToConnectionsReveal);
    }

    [Test]
    public void AWordStillBeingLearnedGetsNoReExposureAfterARightAnswer()
    {
        // It is back within minutes anyway
        Sequencer().Build(2, ReviewGrade.Hard, CardDifficulty.Difficult, headwordLength: 8, learning: true)
            .Should().BeEmpty();

        // A miss still gets its support
        Sequencer().Build(2, ReviewGrade.Again, CardDifficulty.Difficult, headwordLength: 8, learning: true)
            .Should().NotBeEmpty();
    }

    [Test]
    public void AShakyWordBringsBackTheLevelBelow()
    {
        // Probed on production, so the scaffolded level is replayed - its first exercise,
        // with the rest of the level to fall back on for a word that cannot be built that way.
        var steps = Sequencer().Build(3, ReviewGrade.Good, CardDifficulty.Shaky, headwordLength: 8);

        steps.Select(s => s.Type).Should().Equal(ExerciseType.WordToSpellingCover);
        steps[0].Candidates.Should().Equal(
            ExerciseType.WordToSpellingCover,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.TranslationToSentenceScramble,
            ExerciseType.MeaningToWordCuedType);
    }

    [Test]
    public void ADifficultWordBringsBackTwoLevelsInAscendingOrder()
    {
        var steps = Sequencer().Build(3, ReviewGrade.Good, CardDifficulty.Difficult, headwordLength: 8);

        // Each level's first exercise, with the rest of it to fall back on - the typed ones are
        // passed over when the follow-ups are built, as a follow-up is not marked
        steps.Select(s => s.Type).Should().Equal(
            ExerciseType.WordToSpellingCopy,
            ExerciseType.WordToSpellingCover);
    }

    [Test]
    public void AHardAnswerIsTreatedLikeADifficultWordWhateverTheTierSays()
    {
        var steps = Sequencer().Build(3, ReviewGrade.Hard, CardDifficulty.Comfortable, headwordLength: 8);

        steps.Should().HaveCount(2);
    }

    [Test]
    public void AFailureRunsTheDiminishingCuesSequence()
    {
        var steps = Sequencer().Build(3, ReviewGrade.Again, CardDifficulty.Shaky, headwordLength: 10);

        // First what ties the word to things already known, then it is asked again with cues
        // that shrink: one letter, then roughly half the word, then tiles, then the whole thing.
        steps.Select(s => s.Type).Should().Equal(
            ExerciseType.WordToConnectionsReveal,
            ExerciseType.MeaningToWordPartialLetters,
            ExerciseType.MeaningToWordPartialLetters,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.WordToMeaningReveal);

        steps[1].RevealedLetters.Should().Be(1);
        steps[2].RevealedLetters.Should().Be(4);
        steps[2].RevealedLetters.Should().BeLessThan(10, "the word must never be spelled out as its own cue");
    }

    [Test]
    public void AMissThatCostsNothingIsFollowedOnlyByTheWordsConnections()
    {
        // The word comes back shortly to be asked another way, so it is not walked through
        // its letters now.
        var steps = Sequencer().Build(2, ReviewGrade.Again, CardDifficulty.Shaky, headwordLength: 8, tolerated: true);

        steps.Select(s => s.Type).Should().Equal(ExerciseType.WordToConnectionsReveal);
    }

    [Test]
    public void TheDiminishingCuesSequenceRunsEvenFromTheBottomRung()
    {
        // A word failed at rung 0 has nothing below it, but still needs re-encoding.
        Sequencer().Build(0, ReviewGrade.Again, CardDifficulty.Difficult, headwordLength: 6)
            .Should().NotBeEmpty();
    }

    [Test]
    public void ShortWordsSkipThePartialLetterStepThatWouldGiveThemAway()
    {
        var steps = Sequencer().Build(3, ReviewGrade.Again, CardDifficulty.Shaky, headwordLength: 2);

        steps.Count(s => s.Type == ExerciseType.MeaningToWordPartialLetters).Should().Be(1);
    }

    [Test]
    public void ASingleLetterWordGetsNoPartialLetterCueAtAll()
    {
        var steps = Sequencer().Build(3, ReviewGrade.Again, CardDifficulty.Shaky, headwordLength: 1);

        steps.Should().NotContain(s => s.Type == ExerciseType.MeaningToWordPartialLetters);
    }

    [Test]
    public void FollowUpsNeverReachPastTheBottomOfTheLadder()
    {
        var steps = Sequencer().Build(1, ReviewGrade.Good, CardDifficulty.Difficult, headwordLength: 8);

        steps.Select(s => s.Type).Should().Equal(ExerciseType.WordToMeaningReveal);
    }

    [Test]
    public void FollowUpCountsComeFromConfiguration()
    {
        var options = new StudyOptions();
        options.FollowUpsByTier.Shaky = 3;

        Sequencer(options).Build(5, ReviewGrade.Good, CardDifficulty.Shaky, headwordLength: 8)
            .Should().HaveCount(3);
    }
}
