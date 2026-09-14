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
    public void AShakyWordBringsBackTheRungBelow()
    {
        var steps = Sequencer().Build(3, ReviewGrade.Good, CardDifficulty.Shaky, headwordLength: 8);

        steps.Select(s => s.Type).Should().Equal(ExerciseType.MeaningToWordChoice);
    }

    [Test]
    public void ADifficultWordBringsBackTwoRungsInAscendingOrder()
    {
        var steps = Sequencer().Build(3, ReviewGrade.Good, CardDifficulty.Difficult, headwordLength: 8);

        steps.Select(s => s.Type).Should().Equal(
            ExerciseType.WordToMeaningChoice,
            ExerciseType.MeaningToWordChoice);
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

        // Cues shrink: one letter, then roughly half the word, then tiles, then the whole thing.
        steps.Select(s => s.Type).Should().Equal(
            ExerciseType.MeaningToWordPartialLetters,
            ExerciseType.MeaningToWordPartialLetters,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.WordToMeaningReveal);

        steps[0].RevealedLetters.Should().Be(1);
        steps[1].RevealedLetters.Should().Be(4);
        steps[1].RevealedLetters.Should().BeLessThan(10, "the word must never be spelled out as its own cue");
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
