using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class DistractorPickerTests
{
    private static DistractorPicker Picker(StudyOptions? options = null) =>
        new(options ?? new StudyOptions(), new Random(7));

    private static StudyMaterial Target(string partOfSpeech = "noun") => new()
    {
        WordId = 1,
        Headword = "target",
        PartOfSpeech = partOfSpeech,
        Meaning = "the thing being aimed at"
    };

    private static List<DistractorCandidate> Pool(int count, string partOfSpeech = "noun", int startId = 2) =>
        Enumerable.Range(startId, count)
            .Select(i => new DistractorCandidate(i, $"word{i}", partOfSpeech, i * 10, $"meaning {i}"))
            .ToList();

    [Test]
    public void EnoughCandidatesProducesAFullSetForBothDirections()
    {
        var set = Picker().Pick(Target(), Pool(10));

        set.Should().NotBeNull();
        set!.Headwords.Should().HaveCount(3);
        set.Meanings.Should().HaveCount(3);
    }

    [Test]
    public void TooFewCandidatesReturnsNothingSoTheLadderCanFallBack()
    {
        Picker().Pick(Target(), Pool(2)).Should().BeNull();
    }

    [Test]
    public void CandidatesWithoutAMeaningCannotBeUsedAsWrongMeanings()
    {
        var pool = Enumerable.Range(2, 6)
            .Select(i => new DistractorCandidate(i, $"word{i}", "noun", i * 10, Meaning: null))
            .ToList();

        // There are plenty of headwords, but no meanings to choose between.
        Picker().Pick(Target(), pool).Should().BeNull();
    }

    [Test]
    public void TheTargetWordIsNeverOfferedAsItsOwnDistractor()
    {
        var pool = Pool(6);
        pool.Add(new DistractorCandidate(1, "target", "noun", 5, "the thing being aimed at"));

        var set = Picker().Pick(Target(), pool);

        set!.Headwords.Should().NotContain("target");
        set.Meanings.Should().NotContain("the thing being aimed at");
    }

    [Test]
    public void ADifferentWordSharingTheTargetsTextIsAlsoExcluded()
    {
        var pool = Pool(6);
        pool.Add(new DistractorCandidate(99, "Target", "noun", 5, "The Thing Being Aimed At"));

        var set = Picker().Pick(Target(), pool);

        set!.Headwords.Should().NotContain(h => h.Equals("target", StringComparison.OrdinalIgnoreCase));
        set.Meanings.Should().NotContain(m => m.Equals("the thing being aimed at", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void CandidatesSharingThePartOfSpeechAreUsedFirst()
    {
        var pool = Pool(3, "noun", startId: 2).Concat(Pool(10, "verb", startId: 50)).ToList();

        var set = Picker().Pick(Target("noun"), pool);

        // The three nouns should fill the set, leaving the verbs unused.
        set!.Headwords.Should().BeEquivalentTo(new[] { "word2", "word3", "word4" });
    }

    [Test]
    public void OtherPartsOfSpeechTopUpTheSetWhenThereAreNotEnoughMatches()
    {
        var pool = Pool(1, "noun", startId: 2).Concat(Pool(10, "verb", startId: 50)).ToList();

        var set = Picker().Pick(Target("noun"), pool);

        set.Should().NotBeNull();
        set!.Headwords.Should().HaveCount(3).And.Contain("word2");
    }

    [Test]
    public void DistractorsAreDistinct()
    {
        var pool = Pool(10);
        pool.Add(new DistractorCandidate(500, "word2", "noun", 20, "meaning 2"));

        var set = Picker().Pick(Target(), pool);

        set!.Headwords.Should().OnlyHaveUniqueItems();
        set.Meanings.Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void TheSelectionVariesBetweenSittings()
    {
        var pool = Pool(40);

        var first = new DistractorPicker(new StudyOptions(), new Random(1)).Pick(Target(), pool);
        var second = new DistractorPicker(new StudyOptions(), new Random(2)).Pick(Target(), pool);

        first!.Headwords.Should().NotEqual(second!.Headwords);
    }

    [Test]
    public void TheOptionCountComesFromConfiguration()
    {
        var options = new StudyOptions { ChoiceOptionCount = 6 };

        Picker(options).Pick(Target(), Pool(10))!.Headwords.Should().HaveCount(5);

        // Five distractors are needed now, so a pool that was ample before is not.
        Picker(options).Pick(Target(), Pool(4)).Should().BeNull();
    }
}
