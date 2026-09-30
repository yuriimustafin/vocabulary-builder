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
        string? sentence = "The future looks bright for us.",
        string? translation = "Майбутнє виглядає світлим для нас.",
        int? exampleId = 7) => new()
        {
            WordId = 1,
            Headword = "bright",
            Meaning = "full of light",
            ContextSentence = sentence,
            ContextSentenceTranslation = translation,
            ContextForm = "bright",
            ExampleId = exampleId,
            Collocates = new[] { "light", "future", "colours", "idea", "student" },
            NonCollocates = new[] { "silence", "debt", "Tuesday" },
            Etymology = "Old English beorht, shining.",
            Mnemonic = "Sounds like 'brat': a bright brat."
        };

    // --- what goes with it -------------------------------------------------

    private static WordToCollocatesChoiceExerciseDefinition Collocates() => new(Grades, new Random(2));

    [Test]
    public void TheFirstThreeOfEachAreOfferedShuffled()
    {
        var payload = Collocates().Build(Material(), new ExerciseBuildContext());

        payload.Prompt.Should().Be("bright");
        payload.Options.Should().BeEquivalentTo(new[] { "light", "future", "colours", "silence", "debt", "Tuesday" });
        payload.Options.Should().NotEqual(new[] { "light", "future", "colours", "silence", "debt", "Tuesday" });
        payload.GradingMode.Should().Be(GradingMode.Automatic);
    }

    [Test]
    public void TickingExactlyTheRightOnesIsCorrect()
    {
        Collocates().Resolve(new ExerciseAnswer(ElapsedMs: 5000, Selections: new[] { "light", "Future", "colours" }), Material())
            .Should().Be(ReviewGrade.Good);
    }

    [Test]
    public void ItIsRecognitionSoNeverEasyHoweverFast()
    {
        Collocates().Resolve(new ExerciseAnswer(ElapsedMs: 500, Selections: new[] { "light", "future", "colours" }), Material())
            .Should().Be(ReviewGrade.Good);
    }

    [Test]
    public void OneSlipIsHardAndMoreIsAMiss()
    {
        var definition = Collocates();

        definition.Resolve(new ExerciseAnswer(ElapsedMs: 5000, Selections: new[] { "light", "future" }), Material())
            .Should().Be(ReviewGrade.Hard, "one right one left unticked");
        definition.Resolve(new ExerciseAnswer(ElapsedMs: 5000, Selections: new[] { "light", "future", "colours", "debt" }), Material())
            .Should().Be(ReviewGrade.Hard, "one wrong one ticked");
        definition.Resolve(new ExerciseAnswer(ElapsedMs: 5000, Selections: new[] { "light", "debt" }), Material())
            .Should().Be(ReviewGrade.Again);
        definition.Resolve(new ExerciseAnswer(ElapsedMs: 5000, Abandoned: true), Material())
            .Should().Be(ReviewGrade.Again);
    }

    [TestCase("interjection")]
    [TestCase("interj")]
    [TestCase("exclamation")]
    [TestCase("preposition")]
    [TestCase("prép")]
    [TestCase("conjunction")]
    [TestCase("pronoun")]
    [TestCase("article")]
    [TestCase("determiner")]
    [TestCase("numeral")]
    public void AWordWithNoPartnersToSpeakOfIsNeverAskedWhatItGoesWith(string partOfSpeech)
    {
        // "What goes with bonjour?" has no honest answer, whatever the model offered for it
        Collocates().CanBuild(Material() with { PartOfSpeech = partOfSpeech }, null).Should().BeFalse();
    }

    [TestCase("adjective")]
    [TestCase("adj")]
    [TestCase("noun")]
    [TestCase("nf")]
    [TestCase("verb")]
    [TestCase("vtr")]
    [TestCase("adverb")]
    [TestCase(null)]
    public void EveryPartOfSpeechThatCombinesCanBeAsked(string? partOfSpeech)
    {
        Collocates().CanBuild(Material() with { PartOfSpeech = partOfSpeech }, null).Should().BeTrue();
    }

    [Test]
    public void ItNeedsSomethingOnBothSides()
    {
        Collocates().CanBuild(Material() with { NonCollocates = Array.Empty<string>() }, null).Should().BeFalse();
        Collocates().CanBuild(Material() with { Collocates = new[] { "light" } }, null).Should().BeFalse();
        Collocates().CanBuild(Material(), null).Should().BeTrue();
    }

    // --- building the sentence ----------------------------------------------

    private static TranslationToSentenceScrambleExerciseDefinition Sentence() => new(Grades, new Random(2));

    [Test]
    public void TheSentencesWordsAreOfferedShuffledFromItsTranslation()
    {
        var payload = Sentence().Build(Material(), new ExerciseBuildContext());

        payload.Prompt.Should().Be("Майбутнє виглядає світлим для нас.");
        payload.Tiles.Should().BeEquivalentTo(new[] { "the", "future", "looks", "bright", "for", "us" });
        payload.Tiles.Should().NotEqual(new[] { "the", "future", "looks", "bright", "for", "us" });
        payload.ExampleId.Should().Be(7, "rebuilding it counts as practising that sentence");
    }

    [Test]
    public void WithNoTranslationThereIsNothingToRebuildItFrom()
    {
        // The word's meaning is no cue to the order of a sentence never seen
        Sentence().CanBuild(Material(translation: null), null).Should().BeFalse();
        Sentence().CanBuild(Material(), null).Should().BeTrue();
    }

    [Test]
    public void TheSentenceIsMarkedIgnoringCaseAndPunctuation()
    {
        var definition = Sentence();

        definition.Resolve(new ExerciseAnswer("the future looks bright for us", ElapsedMs: 8000), Material())
            .Should().Be(ReviewGrade.Good);
        definition.Resolve(new ExerciseAnswer("the bright future looks for us", ElapsedMs: 8000), Material())
            .Should().Be(ReviewGrade.Again);
        definition.Resolve(new ExerciseAnswer("the future looks bright for us", ElapsedMs: 8000, Resets: 1), Material())
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void OnlyAStoredExampleOfAHandyLengthIsRebuilt()
    {
        Sentence().CanBuild(Material(exampleId: null), null).Should().BeFalse("only a stored example tracks practice");
        Sentence().CanBuild(Material(sentence: "Bright!"), null).Should().BeFalse();
        Sentence().CanBuild(Material(sentence: string.Join(' ', Enumerable.Repeat("very", 15)) + " bright"), null).Should().BeFalse();
        Sentence().CanBuild(Material(), null).Should().BeTrue();
    }

    [Test]
    public void AnElisionStaysOneTileAndACapitalDoesNotGiveTheFirstWordAway()
    {
        TranslationToSentenceScrambleExerciseDefinition.Words("L'école est fermée, hélas !")
            .Should().Equal("l'école", "est", "fermée", "hélas");
        TranslationToSentenceScrambleExerciseDefinition.Words("NASA sent it.")
            .Should().Equal("NASA", "sent", "it");
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
