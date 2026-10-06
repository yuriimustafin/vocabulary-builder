using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Exercises.Definitions;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class ConnectionExerciseDefinitionTests
{
    private static readonly GradeResolver Grades = new(new StudyOptions());

    private static StudyMaterial Material(
        string? sentence = "She has a very bright future ahead of her.",
        string? translation = "Перед нею дуже яскраве майбутнє.",
        int? exampleId = 7) => new()
        {
            WordId = 1,
            Headword = "bright",
            Meaning = "full of light",
            ContextSentence = sentence,
            ContextSentenceTranslation = translation,
            ContextForm = "bright",
            ContextCollocation = "a bright future",
            ContextGlosses = new Dictionary<string, string>
            {
                ["bright"] = "яскраве",
                ["future"] = "майбутнє",
                ["ahead"] = "попереду",
                ["of"] = "від"
            },
            ExampleId = exampleId,
            Etymology = "Old English beorht, shining.",
            Mnemonic = "Sounds like 'brat': a bright brat."
        };

    // --- completing the sentence -----------------------------------------------

    private static TranslationToSentenceScrambleExerciseDefinition Sentence() => new(Grades, new Random(2));

    [Test]
    public void TheGapIsTheWordAndThePhraseItIsUsedInAndTheRestStandsAsWritten()
    {
        var gap = TranslationToSentenceScrambleExerciseDefinition.Gap(Material())!;

        // Four words in a sentence of seven or more, the phrase's own word first; a short
        // word shares a tile with its neighbour, so there are three to order
        gap.Start.Should().Be("She has a very ");
        gap.Tiles.Should().Equal("bright", "future", "ahead of");
        gap.End.Should().Be(" her.");
    }

    [Test]
    public void PunctuationAtTheEdgeOfTheGapStaysWithTheSentence()
    {
        var gap = TranslationToSentenceScrambleExerciseDefinition.Gap(
            Material("Il a perdu son crochet.") with { Headword = "crochet", ContextForm = "crochet", ContextCollocation = null })!;

        gap.Start.Should().Be("Il a ");
        gap.Tiles.Should().Equal("perdu", "son", "crochet");
        gap.End.Should().Be(".", "a full stop on a tile would say which one goes last");
    }

    [Test]
    public void TheTilesAreShuffledAndTheirTranslationsAreAFreeHint()
    {
        var definition = Sentence();
        var payload = definition.Build(Material(), new ExerciseBuildContext());

        payload.Tiles.Should().BeEquivalentTo(new[] { "bright", "future", "ahead of" });
        payload.Tiles.Should().NotEqual(new[] { "bright", "future", "ahead of" });
        payload.SentenceStart.Should().Be("She has a very ");
        payload.SentenceEnd.Should().Be(" her.");
        payload.Prompt.Should().Be("She has a very _____ _____ _____ her.");
        payload.ExampleId.Should().Be(7, "completing it counts as practising that sentence");

        payload.ContextSentenceTranslation.Should().Be("Перед нею дуже яскраве майбутнє.");
        payload.OptionHints.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["bright"] = "яскраве",
            ["future"] = "майбутнє",
            ["ahead of"] = "попереду від"
        });
        definition.HintIsFree.Should().BeTrue();
    }

    [Test]
    public void TheGapIsMarkedIgnoringCaseAndPunctuation()
    {
        var definition = Sentence();

        definition.Resolve(new ExerciseAnswer("Bright future ahead of", ElapsedMs: 6000), Material())
            .Should().Be(ReviewGrade.Good);
        definition.Resolve(new ExerciseAnswer("future bright ahead of", ElapsedMs: 6000), Material())
            .Should().Be(ReviewGrade.Again);
        definition.Resolve(new ExerciseAnswer("bright future ahead of", ElapsedMs: 6000, Resets: 1), Material())
            .Should().Be(ReviewGrade.Hard, "starting over means the order was not known");
        definition.Resolve(new ExerciseAnswer("bright future ahead of", ElapsedMs: 40_000, FreeHintTaken: true), Material())
            .Should().Be(ReviewGrade.Good, "reading the free hint takes time, and that is not held against it");
    }

    [Test]
    public void OnlyAStoredTranslatedSentenceWithSomethingLeftStandingIsOffered()
    {
        var definition = Sentence();

        definition.CanBuild(Material(), null).Should().BeTrue();
        definition.CanBuild(Material(exampleId: null), null).Should().BeFalse("only a stored example's practice is tracked");
        definition.CanBuild(Material(translation: null), null).Should().BeFalse("the hint needs a translation");
        definition.CanBuild(Material("A bright one.") with { ContextCollocation = null }, null)
            .Should().BeFalse("three words is the whole gap, with nothing left as written");
    }

    // --- remembering it by ---------------------------------------------------

    [Test]
    public void TheConnectionsCardShowsTheWordItsMeaningAndWhatTiesItToThingsKnown()
    {
        var payload = new WordToConnectionsRevealExerciseDefinition().Build(Material(), new ExerciseBuildContext());

        payload.Prompt.Should().Be("bright");
        payload.Answer.Should().Be("full of light");
        payload.Connections!.Mnemonic.Should().StartWith("Sounds like");
        payload.Connections.Etymology.Should().StartWith("Old English");
    }

    [Test]
    public void AWordWithNothingToTieItToHasNoConnectionsCard()
    {
        new WordToConnectionsRevealExerciseDefinition()
            .CanBuild(Material() with { Etymology = null, Mnemonic = null, Cognates = null }, null)
            .Should().BeFalse();
    }
}
