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

    private static Sense GlossedSense(
        string definition, string? gloss, IList<string>? examples = null, IList<string>? translations = null) => new()
    {
        Definition = definition,
        Gloss = gloss,
        Examples = examples ?? new List<string>(),
        ExampleTranslations = translations
    };

    /// <summary>
    /// The gloss says which sense the meaning is, so it has to come from the sense that
    /// supplied the meaning - not from whichever sense happens to be first.
    /// </summary>
    [Test]
    public void ShouldTakeTheGlossFromTheSenseThatGaveTheMeaning()
    {
        var word = Word(senses: new List<Sense>
        {
            GlossedSense("", "ignored, this one says nothing"),
            GlossedSense("good evening", "pour dire bonjour en soirée"),
            GlossedSense("another meaning", "un autre sens")
        });

        var material = Resolver().Resolve(word, null);

        material.Meaning.Should().Be("good evening");
        material.MeaningGloss.Should().Be("pour dire bonjour en soirée");
    }

    /// <summary>
    /// The meaning is what gets compared against other meanings, so nothing may be wrapped
    /// around it - the gloss travels beside it, not in front of it.
    /// </summary>
    [Test]
    public void ShouldLeaveTheMeaningWithNothingWrappedAroundIt()
    {
        var word = Word(senses: new List<Sense>
        {
            GlossedSense("good evening, hello", "pour dire bonjour en soirée")
        });

        Resolver().Resolve(word, null).Meaning.Should().Be("good evening, hello");
    }

    [Test]
    public void ShouldHaveNoGlossWhenTheSourceGaveNone()
    {
        var word = Word(senses: new List<Sense> { GlossedSense("a long walk", null) });

        Resolver().Resolve(word, null).MeaningGloss.Should().BeNull();
    }

    /// <summary>
    /// A generated definition is not a dictionary sense and has nothing to gloss it.
    /// </summary>
    [Test]
    public void ShouldHaveNoGlossForAGeneratedMeaning()
    {
        var material = Resolver().Resolve(Word(), Generated(definition: "a generated meaning"));

        material.Meaning.Should().Be("a generated meaning");
        material.MeaningGloss.Should().BeNull();
    }

    [Test]
    public void ShouldTakeTheTranslationOfTheSentenceItChose()
    {
        var word = Word(headword: "bonsoir", senses: new List<Sense>
        {
            GlossedSense(
                "good evening",
                "pour dire bonjour en soirée",
                new List<string> { "Rien à voir ici.", "Bonsoir à tous et bienvenue !" },
                new List<string> { "Nothing to do with it.", "Good evening everyone and welcome!" })
        });

        var material = Resolver().Resolve(word, null);

        // The first sentence has no headword in it, so the second is the one used - and its
        // translation has to be the second one too
        material.ContextSentence.Should().Be("Bonsoir à tous et bienvenue !");
        material.ContextSentenceTranslation.Should().Be("Good evening everyone and welcome!");
    }

    /// <summary>
    /// A cloze blanks the sentence, so the sentence must be the sentence and nothing else.
    /// </summary>
    [Test]
    public void ShouldKeepTheTranslationOutOfTheSentence()
    {
        var word = Word(headword: "bonsoir", senses: new List<Sense>
        {
            GlossedSense(
                "good evening",
                null,
                new List<string> { "Bonsoir à tous !" },
                new List<string> { "Good evening everyone!" })
        });

        Resolver().Resolve(word, null).ContextSentence.Should().NotContain("Good evening");
    }

    [Test]
    public void ShouldUseTheSentenceAloneWhenNobodyTranslatedIt()
    {
        var word = Word(headword: "bonsoir", senses: new List<Sense>
        {
            GlossedSense("good evening", null, new List<string> { "Bonsoir à tous !" })
        });

        var material = Resolver().Resolve(word, null);

        material.ContextSentence.Should().Be("Bonsoir à tous !");
        material.ContextSentenceTranslation.Should().BeNull();
    }

    /// <summary>
    /// A translation list shorter than the sentences it belongs to must not throw.
    /// </summary>
    [Test]
    public void ShouldCopeWithFewerTranslationsThanSentences()
    {
        var word = Word(headword: "bonsoir", senses: new List<Sense>
        {
            GlossedSense(
                "good evening",
                null,
                new List<string> { "Rien ici.", "Bonsoir à tous !" },
                new List<string> { "Nothing here." })
        });

        var material = Resolver().Resolve(word, null);

        material.ContextSentence.Should().Be("Bonsoir à tous !");
        material.ContextSentenceTranslation.Should().BeNull();
    }

    [Test]
    public void AFrenchNounCarriesItsArticle()
    {
        var word = Word(headword: "arbre");
        word.Language = Language.French;
        word.Gender = GrammaticalGender.Masculine;

        var material = Resolver().Resolve(word, generated: null);

        // Kept apart from the headword, which answers are marked against
        material.Headword.Should().Be("arbre");
        material.Article!.Definite.Should().Be("l'");
        material.Article.Indefinite.Should().Be("un");
        material.Article.Gender.Should().Be("masculine");
        material.Article.IsElided.Should().BeTrue();
    }

    [Test]
    public void AWordWithoutAGenderHasNoArticle()
    {
        var english = Word(headword: "tree");
        var frenchVerb = Word(headword: "prendre");
        frenchVerb.Language = Language.French;

        Resolver().Resolve(english, generated: null).Article.Should().BeNull();
        Resolver().Resolve(frenchVerb, generated: null).Article.Should().BeNull();
    }

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
        const StudyMaterialGaps NeverGenerated = StudyMaterialGaps.Examples | StudyMaterialGaps.Connections;

        resolver.FindGaps(Word(), null)
            .Should().Be(StudyMaterialGaps.Meaning | StudyMaterialGaps.ContextSentence | NeverGenerated);

        resolver.FindGaps(Word(senses: new List<Sense> { Sense("found everywhere") }), null)
            .Should().Be(StudyMaterialGaps.ContextSentence | NeverGenerated);

        resolver.FindGaps(
                Word(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") }), null)
            .Should().Be(NeverGenerated, "every word is asked, once, for examples and connections");
    }

    [Test]
    public void AWordFilledInForTheCurrentPromptWithEnoughExamplesMissesNothing()
    {
        var content = Generated("found everywhere");
        content.PromptVersion = VocabularyBuilder.Application.Study.Enrichment.StudyContentPrompt.Version;

        Resolver().FindGaps(Word(), content, Examples(Example(1, "Screens are ubiquitous."), Example(2, "Ubiquitous ads."),
                Example(3, "It is ubiquitous here.")))
            .Should().Be(StudyMaterialGaps.None);
    }

    [Test]
    public void ContentFromAnOlderPromptIsMissingItsConnections()
    {
        var content = Generated("found everywhere");
        content.PromptVersion = "v2";

        Resolver().FindGaps(Word(), content).Should().HaveFlag(StudyMaterialGaps.Connections);
    }

    // --- stored examples ------------------------------------------------------

    private static StudyExample Example(int id, string sentence, string form = "ubiquitous", int successes = 0,
        DateTime? lastUsed = null) => new()
        {
            Id = id,
            WordId = 1,
            Sentence = sentence,
            Form = form,
            Translation = $"translation {id}",
            Successes = successes,
            LastUsedAtUtc = lastUsed
        };

    private static StudyExampleSet Examples(params StudyExample[] examples) => new(examples, Array.Empty<string>());

    [Test]
    public void AStoredExampleComesBeforeADictionaryOne()
    {
        var word = Word(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") });

        var material = Resolver().Resolve(word, null, Examples(Example(7, "Ubiquitous coffee shops line the street.")));

        material.ContextSentence.Should().Be("Ubiquitous coffee shops line the street.");
        material.ContextSentenceTranslation.Should().Be("translation 7");
        material.ExampleId.Should().Be(7);
    }

    [Test]
    public void AnExampleNotYetAnsweredComesBeforeOneThatHasBeen()
    {
        var material = Resolver().Resolve(Word(), null, Examples(
            Example(1, "Screens are ubiquitous.", successes: 2),
            Example(2, "Ubiquitous ads everywhere.")));

        material.ExampleId.Should().Be(2);
    }

    [Test]
    public void AnExampleForAFormMetButNotYetPractisedComesFirst()
    {
        var word = Word("prendre");
        var set = new StudyExampleSet(
            new[]
            {
                Example(1, "Je vais prendre le bus.", "prendre"),
                Example(2, "Elle prend le train.", "prend"),
                Example(3, "Il a pris froid.", "pris", successes: 1)
            },
            new[] { "prend", "pris" });

        Resolver().Resolve(word, null, set).ExampleId.Should().Be(2, "prend was met, and has not been practised");
    }

    [Test]
    public void AmongPractisedExamplesTheLeastPractisedAndLongestUnusedComesFirst()
    {
        var material = Resolver().Resolve(Word(), null, Examples(
            Example(1, "Screens are ubiquitous.", successes: 1, lastUsed: new DateTime(2026, 9, 20)),
            Example(2, "Ubiquitous ads everywhere.", successes: 1, lastUsed: new DateTime(2026, 9, 10)),
            Example(3, "It is ubiquitous here.", successes: 3, lastUsed: new DateTime(2026, 9, 1))));

        material.ExampleId.Should().Be(2);
    }

    [Test]
    public void TheExampleAnExerciseWasAskedOnIsUsedWhenMarkingIt()
    {
        var set = new StudyExampleSet(
            new[] { Example(1, "Screens are ubiquitous."), Example(2, "Ubiquitous ads everywhere.") },
            Array.Empty<string>(),
            PreferredId: 2);

        Resolver().Resolve(Word(), null, set).ExampleId.Should().Be(2);
    }

    [Test]
    public void AnExampleThatDoesNotContainItsFormIsNeverUsed()
    {
        var material = Resolver().Resolve(Word(), null, Examples(Example(1, "You see them everywhere.")));

        material.HasContextSentence.Should().BeFalse();
        material.ExampleId.Should().BeNull();
    }

    [Test]
    public void TheClozeBlanksTheFormTheSentenceUses()
    {
        var material = Resolver().Resolve(Word("prendre"), null, Examples(Example(1, "Elle prend le train.", "prend")));

        material.ContextForm.Should().Be("prend");
        material.BlankedContextSentence.Should().Be("Elle _____ le train.");
    }

    [Test]
    public void AFormWithNoExampleIsReportedAsUncovered()
    {
        var set = new StudyExampleSet(new[] { Example(1, "Elle prend le train.", "prend") }, new[] { "prend", "pris" });

        Resolver().UncoveredForms(Word("prendre"), set).Should().Equal("pris");
        Resolver().FindGaps(Word("prendre"), null, set).Should().HaveFlag(StudyMaterialGaps.Forms);
    }

    [Test]
    public void TheHeadwordIsCoveredByADictionaryExampleThatUsesIt()
    {
        var word = Word(senses: new List<Sense> { Sense("found everywhere", "Screens are ubiquitous.") });
        var set = new StudyExampleSet(Array.Empty<StudyExample>(), new[] { "ubiquitous" });

        Resolver().UncoveredForms(word, set).Should().BeEmpty();
    }

    [Test]
    public void TheConnectionsComeFromTheGeneratedContent()
    {
        var content = Generated("found everywhere");
        content.Etymology = "From Latin ubique, everywhere.";
        content.Mnemonic = "Sounds like 'you-bick'.";

        var connections = Resolver().Resolve(Word(), content).Connections;

        connections!.Etymology.Should().Be("From Latin ubique, everywhere.");
        connections.Mnemonic.Should().Be("Sounds like 'you-bick'.");
        connections.Usage.Should().BeNull();
        Resolver().Resolve(Word(), Generated("found everywhere")).Connections.Should().BeNull("there is nothing to show");
    }

    [Test]
    public void BlankAndWhitespaceOnlyDefinitionsAreTreatedAsMissing()
    {
        var word = Word(senses: new List<Sense> { Sense("   ") });

        Resolver().Resolve(word, null).HasMeaning.Should().BeFalse();
    }
}
