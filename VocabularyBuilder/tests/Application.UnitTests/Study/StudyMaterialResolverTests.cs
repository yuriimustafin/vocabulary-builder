using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class StudyMaterialResolverTests
{
    private static StudyMaterialResolver Resolver() => new();

    private static Word Word(
        string headword = "ubiquitous",
        IList<Sense>? senses = null,
        IList<string>? examples = null) => new()
        {
            Id = 1,
            Headword = headword,
            PartOfSpeech = "adjective",
            Senses = senses,
            Examples = examples
        };

    private static Sense Sense(string definition, params string[] examples) => new()
    {
        Definition = definition,
        Examples = examples.ToList()
    };

    private static WordStudyContent Generated(string? definition = null, string? sentence = null) => new()
    {
        WordId = 1,
        Status = StudyContentStatus.Ready,
        GeneratedDefinition = definition,
        GeneratedContextSentence = sentence
    };

    [Test]
    public void DictionaryDataIsUsedInPreferenceToGeneratedContent()
    {
        var word = Word(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous now.") });

        var material = Resolver().Resolve(word, Generated("a generated definition", "A generated ubiquitous line."));

        material.Meaning.Should().Be("found everywhere");
        material.ContextSentence.Should().Be("Screens are ubiquitous now.");
    }

    [Test]
    public void GeneratedContentFillsOnlyWhatIsMissing()
    {
        // A word with a definition but no usable example takes only the sentence.
        var word = Word(senses: new List<Sense> { Sense("found everywhere") });

        var material = Resolver().Resolve(word, Generated("a generated definition", "Plastic is ubiquitous."));

        material.Meaning.Should().Be("found everywhere");
        material.ContextSentence.Should().Be("Plastic is ubiquitous.");
    }

    [Test]
    public void ExamplesOnTheWordItselfAreUsedWhenSensesCarryNone()
    {
        var word = Word(
            senses: new List<Sense> { Sense("found everywhere") },
            examples: new List<string> { "Coffee shops are ubiquitous here." });

        Resolver().Resolve(word, null).ContextSentence.Should().Be("Coffee shops are ubiquitous here.");
    }

    [Test]
    public void AnExampleThatDoesNotContainTheHeadwordIsRejected()
    {
        // Nothing to blank out means it is useless as a cloze, whatever else it is good for.
        var word = Word(senses: new List<Sense> { Sense("found everywhere", "It is everywhere you look.") });

        Resolver().Resolve(word, null).HasContextSentence.Should().BeFalse();
    }

    [Test]
    public void AGeneratedSentenceIsHeldToTheSameRule()
    {
        var word = Word(senses: new List<Sense> { Sense("found everywhere") });

        var material = Resolver().Resolve(word, Generated(sentence: "You see them absolutely everywhere."));

        material.HasContextSentence.Should().BeFalse();
    }

    [Test]
    public void TheHeadwordIsMatchedWholeWordAndCaseInsensitively()
    {
        var word = Word("apt", senses: new List<Sense> { Sense("suitable", "That is an Apt description.") });

        Resolver().Resolve(word, null).ContextSentence.Should().Be("That is an Apt description.");
    }

    [Test]
    public void AHeadwordAppearingOnlyInsideALongerWordDoesNotCount()
    {
        var word = Word("apt", senses: new List<Sense> { Sense("suitable", "He showed real aptitude.") });

        Resolver().Resolve(word, null).HasContextSentence.Should().BeFalse();
    }

    [Test]
    public void BlankingReplacesOnlyTheFirstOccurrence()
    {
        var blanked = HeadwordText.Blankify("A ubiquitous and ubiquitous thing.", "ubiquitous");

        blanked.Should().Be($"A {HeadwordText.Blank} and ubiquitous thing.");
    }

    [Test]
    public void AccentedHeadwordsAreMatched()
    {
        var word = Word("élève", senses: new List<Sense> { Sense("pupil", "L'élève travaille bien.") });

        Resolver().Resolve(word, null).HasContextSentence.Should().BeTrue();
    }

    [Test]
    public void GapsReportWhatIsStillMissing()
    {
        var resolver = Resolver();

        resolver.FindGaps(Word(), null)
            .Should().Be(StudyMaterialGaps.Meaning | StudyMaterialGaps.ContextSentence);

        resolver.FindGaps(Word(senses: new List<Sense> { Sense("found everywhere") }), null)
            .Should().Be(StudyMaterialGaps.ContextSentence);

        resolver.FindGaps(
                Word(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") }), null)
            .Should().Be(StudyMaterialGaps.None);
    }

    [Test]
    public void BlankAndWhitespaceOnlyDefinitionsAreTreatedAsMissing()
    {
        var word = Word(senses: new List<Sense> { Sense("   ") });

        Resolver().Resolve(word, null).HasMeaning.Should().BeFalse();
    }
}
