using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Exercises.Definitions;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

public class ExerciseDefinitionTests
{
    private static readonly StudyOptions Options = new();
    private static readonly GradeResolver Grades = new(Options);
    private static readonly Random Seeded = new(3);

    private static StudyMaterial Material(
        string headword = "ubiquitous",
        string? meaning = "found everywhere",
        string? sentence = "Screens are ubiquitous now.") => new()
        {
            WordId = 1,
            Headword = headword,
            PartOfSpeech = "adjective",
            Transcription = "juːˈbɪkwɪtəs",
            Meaning = meaning,
            ContextSentence = sentence
        };

    private static DistractorSet Distractors() => new(
        new[] { "wrong one", "wrong two", "wrong three" },
        new[] { "alpha", "beta", "gamma" });

    // --- a meaning that is the word itself ----------------------------------

    [TestCase("poison", "poison")]
    [TestCase("information", "Information")]
    [TestCase("hôtel", "a hotel")]
    [TestCase("poison", "poison, venom")]
    [TestCase("chat", "cat; chat (informal)")]
    public void AMeaningThatIsTheWordItselfGivesItAway(string headword, string meaning)
    {
        Material(headword, meaning).MeaningGivesAwayWord.Should().BeTrue();
    }

    [TestCase("poisson", "fish")]
    [TestCase("ubiquitous", "found everywhere")]
    [TestCase("vivid", "the meaning of vivid", TestName = "AMeaningThatOnlyMentionsTheWordDoesNot")]
    public void AnOrdinaryMeaningDoesNotGiveTheWordAway(string headword, string meaning)
    {
        Material(headword, meaning).MeaningGivesAwayWord.Should().BeFalse();
    }

    [Test]
    public void NothingIsAskedFromOrForAMeaningThatIsTheWordItself()
    {
        // le poison, "poison": the question would show its own answer
        var material = Material("poison", "poison", "Le poison de ce serpent est mortel.");
        var distractors = Distractors();

        new MeaningToWordChoiceExerciseDefinition(Options, Grades, Seeded).CanBuild(material, distractors).Should().BeFalse();
        new WordToMeaningChoiceExerciseDefinition(Options, Grades, Seeded).CanBuild(material, distractors).Should().BeFalse();
        new MeaningToWordScrambleExerciseDefinition(Options, Grades, Seeded).CanBuild(material, null).Should().BeFalse();
        new MeaningToWordTypeExerciseDefinition(Grades).CanBuild(material, null).Should().BeFalse();
        new MeaningToWordCuedTypeExerciseDefinition(Grades).CanBuild(material, null).Should().BeFalse();
        new MeaningToWordRecallExerciseDefinition().CanBuild(material, null).Should().BeFalse();

        // A sentence still asks something, and meeting the word still shows what it means
        new ContextToWordChoiceExerciseDefinition(Options, Grades, Seeded).CanBuild(material, distractors).Should().BeTrue();
        new ContextToWordRecallExerciseDefinition().CanBuild(material, null).Should().BeTrue();
        new WordToMeaningRevealExerciseDefinition().CanBuild(material, null).Should().BeTrue();
    }

    // --- flashcard ---------------------------------------------------------

    [Test]
    public void TheFlashcardShowsTheWordAndRevealsTheMeaning()
    {
        var payload = new WordToMeaningRevealExerciseDefinition().Build(Material(), new ExerciseBuildContext());

        payload.Prompt.Should().Be("ubiquitous");
        payload.Answer.Should().Be("found everywhere");
        payload.GradingMode.Should().Be(GradingMode.SelfReported);
    }

    [Test]
    public void TheFlashcardNeedsOnlyAMeaningSoItCanAlwaysBeTheFallback()
    {
        var definition = new WordToMeaningRevealExerciseDefinition();

        definition.CanBuild(Material(sentence: null), null).Should().BeTrue();
        definition.CanBuild(Material(meaning: null), null).Should().BeFalse();
    }

    [Test]
    public void ASelfGradedExerciseWithNoJudgementCountsAsAFailure()
    {
        var definition = new WordToMeaningRevealExerciseDefinition();

        definition.Resolve(new ExerciseAnswer(), Material()).Should().Be(ReviewGrade.Again);
        definition.Resolve(new ExerciseAnswer(SelfGrade: ReviewGrade.Easy), Material()).Should().Be(ReviewGrade.Easy);
    }

    // --- multiple choice ---------------------------------------------------

    [Test]
    public void RecognitionOffersTheMeaningsAndMarksAgainstTheCorrectOne()
    {
        var definition = new WordToMeaningChoiceExerciseDefinition(Options, Grades, Seeded);

        var payload = definition.Build(Material(), new ExerciseBuildContext(Distractors()));

        payload.Prompt.Should().Be("ubiquitous");
        payload.Options.Should().HaveCount(4).And.Contain("found everywhere");

        definition.Resolve(new ExerciseAnswer("found everywhere", ElapsedMs: 6_000), Material())
            .Should().Be(ReviewGrade.Good);
        definition.Resolve(new ExerciseAnswer("wrong one", ElapsedMs: 6_000), Material())
            .Should().Be(ReviewGrade.Again);
    }

    [Test]
    public void TheReverseDirectionOffersTheWordsAndHidesThePronunciation()
    {
        var definition = new MeaningToWordChoiceExerciseDefinition(Options, Grades, Seeded);

        var payload = definition.Build(Material(), new ExerciseBuildContext(Distractors()));

        payload.Prompt.Should().Be("found everywhere");
        payload.Options.Should().HaveCount(4).And.Contain("ubiquitous");
        payload.Transcription.Should().BeNull("the pronunciation would give the answer away");
    }

    [Test]
    public void APayloadNeverCarriesTheAnswerForAnAutomaticallyGradedExercise()
    {
        var definition = new WordToMeaningChoiceExerciseDefinition(Options, Grades, Seeded);

        definition.Build(Material(), new ExerciseBuildContext(Distractors())).Answer.Should().BeNull();
    }

    [Test]
    public void MultipleChoiceCannotBeBuiltWithoutEnoughDistractors()
    {
        var definition = new WordToMeaningChoiceExerciseDefinition(Options, Grades, Seeded);

        definition.CanBuild(Material(), null).Should().BeFalse();
        definition.CanBuild(Material(), new DistractorSet(new[] { "only one" }, new[] { "only one" }))
            .Should().BeFalse();
        definition.CanBuild(Material(), Distractors()).Should().BeTrue();
    }

    [Test]
    public void OptionsAreShuffledSoTheAnswerIsNotAlwaysInTheSamePlace()
    {
        var material = Material();
        var positions = Enumerable.Range(1, 30)
            .Select(seed => new WordToMeaningChoiceExerciseDefinition(Options, Grades, new Random(seed)))
            .Select(d => d.Build(material, new ExerciseBuildContext(Distractors())))
            .Select(p => p.Options!.ToList().IndexOf("found everywhere"))
            .Distinct();

        positions.Should().HaveCountGreaterThan(1);
    }

    // --- cloze -------------------------------------------------------------

    [Test]
    public void ClozeBlanksTheWordOutOfItsSentenceAndOffersTheMeaningAsAHint()
    {
        var payload = new ContextToWordRecallExerciseDefinition()
            .Build(Material(), new ExerciseBuildContext());

        payload.Prompt.Should().Be($"Screens are {HeadwordText.Blank} now.");
        payload.Prompt.Should().NotContain("ubiquitous");
        payload.Hint.Should().Be("found everywhere");
        payload.HintAvailable.Should().BeTrue();
    }

    [Test]
    public void AnEscalatedProbeWithholdsTheHintSoTheGradeIsNotInflated()
    {
        var payload = new ContextToWordRecallExerciseDefinition()
            .Build(Material(), new ExerciseBuildContext(AllowHint: false));

        payload.Hint.Should().BeNull();
        payload.HintAvailable.Should().BeFalse();
    }

    [Test]
    public void ClozeNeedsASentenceContainingTheWord()
    {
        var definition = new ContextToWordRecallExerciseDefinition();

        definition.CanBuild(Material(), null).Should().BeTrue();
        definition.CanBuild(Material(sentence: null), null).Should().BeFalse();
    }

    // --- scramble ----------------------------------------------------------

    [Test]
    public void ScrambleOffersEveryLetterOfTheWordAsATile()
    {
        var definition = new MeaningToWordScrambleExerciseDefinition(Options, Grades, Seeded);

        var payload = definition.Build(Material("apt"), new ExerciseBuildContext());

        payload.Prompt.Should().Be("found everywhere");
        payload.Tiles.Should().BeEquivalentTo(new[] { "a", "p", "t" });
        payload.Answer.Should().BeNull("the tiles are the exercise; spelling it out would defeat it");
    }

    [TestCase("remember", new[] { "re", "mem", "ber" })]
    [TestCase("information", new[] { "in", "for", "ma", "tion" })]
    public void ALongerWordIsOfferedAsItsSyllablesWhenTheyMakeThreeOrFour(string headword, string[] chunks)
    {
        var payload = new MeaningToWordScrambleExerciseDefinition(Options, Grades, new Random(1))
            .Build(Material(headword), new ExerciseBuildContext());

        payload.Tiles.Should().BeEquivalentTo(chunks);
        payload.Tiles.Should().NotEqual(chunks, "pieces already in order ask nothing");
    }

    [TestCase("window")]
    [TestCase("bright")]
    [TestCase("ice cream")]
    [TestCase("approfondissement")]
    public void AnyWordOfMoreThanThreeLettersComesInThreeOrFourPiecesThatJoinBackIntoIt(string headword)
    {
        var tiles = new MeaningToWordScrambleExerciseDefinition(Options, Grades, Seeded)
            .Build(Material(headword), new ExerciseBuildContext()).Tiles!;

        tiles.Count.Should().BeInRange(3, 4);
        tiles.Should().OnlyContain(t => t.Length > 0);
        string.Concat(Syllabifier.Chunks(headword, Domain.Enums.Language.English))
            .Should().Be(headword.Replace(" ", string.Empty));
    }

    [Test]
    public void ScrambleMarksTheAssembledWordIgnoringSpacingInAPhrase()
    {
        var definition = new MeaningToWordScrambleExerciseDefinition(Options, Grades, Seeded);
        var material = Material("ice cream");

        definition.Resolve(new ExerciseAnswer("icecream", ElapsedMs: 2_000), material).Should().Be(ReviewGrade.Easy);
        definition.Resolve(new ExerciseAnswer("creamice", ElapsedMs: 2_000), material).Should().Be(ReviewGrade.Again);
    }

    [Test]
    public void RestartingTheScrambleCostsTheGradeEvenWhenItEndsUpRight()
    {
        var definition = new MeaningToWordScrambleExerciseDefinition(Options, Grades, Seeded);

        definition.Resolve(new ExerciseAnswer("ubiquitous", ElapsedMs: 1_000, Resets: 1), Material())
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void ASingleLetterWordCannotBeScrambled()
    {
        new MeaningToWordScrambleExerciseDefinition(Options, Grades, Seeded)
            .CanBuild(Material("a"), null).Should().BeFalse();
    }

    [Test]
    public void AccentedLettersStayOnOneTile()
    {
        var definition = new MeaningToWordScrambleExerciseDefinition(Options, Grades, Seeded);

        // In pieces now, but an accent never comes apart from its letter
        definition.Build(Material("élève"), new ExerciseBuildContext()).Tiles
            .Should().BeEquivalentTo(new[] { "é", "lè", "ve" });
    }

    // --- free production ---------------------------------------------------

    [Test]
    public void ProductionShowsOnlyTheMeaningAndNeverAHint()
    {
        var payload = new MeaningToWordRecallExerciseDefinition()
            .Build(Material(), new ExerciseBuildContext(AllowHint: true));

        payload.Prompt.Should().Be("found everywhere");
        payload.Answer.Should().Be("ubiquitous");
        payload.Hint.Should().BeNull("this rung exists precisely to measure unaided recall");
    }

    // --- diminishing cues --------------------------------------------------

    [Test]
    public void PartialLettersIsNeverChosenAsTheGradedAttempt()
    {
        new MeaningToWordPartialLettersExerciseDefinition().CanBeProbe.Should().BeFalse();
    }

    [Test]
    public void PartialLettersRevealsAsManyLettersAsItIsAsked()
    {
        var definition = new MeaningToWordPartialLettersExerciseDefinition();

        definition.Build(Material("apt"), new ExerciseBuildContext(RevealedLetters: 1)).LetterMask
            .Should().Be("a _ _");
        definition.Build(Material("apt"), new ExerciseBuildContext(RevealedLetters: 2)).LetterMask
            .Should().Be("a p _");
    }

    [Test]
    public void PartialLettersMarksWordBoundariesInAPhraseWithoutHidingThem()
    {
        var mask = new MeaningToWordPartialLettersExerciseDefinition()
            .Build(Material("ice cream"), new ExerciseBuildContext(RevealedLetters: 1)).LetterMask;

        mask.Should().Be("i _ _ / _ _ _ _ _");
    }

    // --- spelling it -----------------------------------------------------------

    [Test]
    public void CopyingShowsTheWordAndWhatItMeans()
    {
        var payload = new WordToSpellingCopyExerciseDefinition().Build(Material("remember"), new ExerciseBuildContext());

        payload.Prompt.Should().Be("remember");
        payload.Meaning.Should().Be("found everywhere");
        payload.Tiles.Should().BeNull("it is copied whole, while it stays on screen");
    }

    [Test]
    public void ALongWordIsCoveredAPieceAtATimeAndAShortOneWhole()
    {
        var cover = new WordToSpellingCoverExerciseDefinition();

        cover.Build(Material("remember"), new ExerciseBuildContext()).Tiles.Should().Equal("re", "mem", "ber");
        cover.Build(Material("apple"), new ExerciseBuildContext()).Tiles.Should().BeNull("five letters are taken in one look");
    }

    [Test]
    public void SpellingIsMarkedLikeTypingButNeverOnSpeed()
    {
        var copy = new WordToSpellingCopyExerciseDefinition();

        copy.Resolve(new ExerciseAnswer("remember", ElapsedMs: 60_000), Material("remember")).Should().Be(ReviewGrade.Good);
        copy.Resolve(new ExerciseAnswer("rememebr", ElapsedMs: 5000), Material("remember")).Should().Be(ReviewGrade.Hard);
        copy.Resolve(new ExerciseAnswer("forget", ElapsedMs: 5000), Material("remember")).Should().Be(ReviewGrade.Again);
        copy.Resolve(new ExerciseAnswer(Abandoned: true), Material("remember")).Should().Be(ReviewGrade.Again);
    }

    [Test]
    public void APhraseWrittenAPieceAtATimeGetsItsSpacesBack()
    {
        // The pieces drop the space, so "ice cream" comes back as "icecream"
        new WordToSpellingCoverExerciseDefinition()
            .Resolve(new ExerciseAnswer("icecream", ElapsedMs: 5000), Material("ice cream"))
            .Should().Be(ReviewGrade.Good);
    }

    // --- typing ------------------------------------------------------------

    [Test]
    public void TypingShowsOnlyTheMeaningAndNeverTheWord()
    {
        var payload = new MeaningToWordTypeExerciseDefinition(Grades).Build(Material(), new ExerciseBuildContext());

        payload.Prompt.Should().Be("found everywhere");
        payload.Answer.Should().BeNull();
        payload.LetterMask.Should().BeNull();
        payload.ContextSentence.Should().BeNull("the sentence contains the word");
        payload.GradingMode.Should().Be(GradingMode.Automatic);
    }

    [Test]
    public void AnExactTypedAnswerIsGradedOnSpeedLikeAnyProduction()
    {
        var definition = new MeaningToWordTypeExerciseDefinition(Grades);

        definition.Resolve(new ExerciseAnswer("Ubiquitous ", ElapsedMs: 2000), Material()).Should().Be(ReviewGrade.Easy);
        definition.Resolve(new ExerciseAnswer("ubiquitous", ElapsedMs: 6000), Material()).Should().Be(ReviewGrade.Good);
    }

    [Test]
    public void ANearMissTypedAnswerIsHardAndAWrongOneAgain()
    {
        var definition = new MeaningToWordTypeExerciseDefinition(Grades);

        definition.Resolve(new ExerciseAnswer("ubiquitus", ElapsedMs: 4000), Material()).Should().Be(ReviewGrade.Hard);
        definition.Resolve(new ExerciseAnswer("everywhere", ElapsedMs: 4000), Material()).Should().Be(ReviewGrade.Again);
        definition.Resolve(new ExerciseAnswer("ubiquitous", ElapsedMs: 4000, Abandoned: true), Material())
            .Should().Be(ReviewGrade.Again);
    }

    [Test]
    public void TypingALongWordSlowlyIsNotMarkedDownForItsLength()
    {
        // Twelve seconds is slow for a click but fair for a fourteen-letter word.
        var definition = new MeaningToWordTypeExerciseDefinition(Grades);

        definition.Resolve(new ExerciseAnswer("extraterrestre", ElapsedMs: 12_000), Material("extraterrestre"))
            .Should().Be(ReviewGrade.Good);
    }

    [Test]
    public void TheCuedTypeShowsTheFirstLettersOfTheWord()
    {
        var payload = new MeaningToWordCuedTypeExerciseDefinition(Grades)
            .Build(Material("ubiquitous"), new ExerciseBuildContext(CueLevel: 0));

        payload.LetterMask.Should().Be("u b i q _ _ _ _ _ _");
    }

    [Test]
    public void TheCuedTypeShowsOnlyTheFirstLetterOnceTheWordHasGotFurther()
    {
        var payload = new MeaningToWordCuedTypeExerciseDefinition(Grades)
            .Build(Material("ubiquitous"), new ExerciseBuildContext(CueLevel: 3));

        payload.LetterMask.Should().Be("u _ _ _ _ _ _ _ _ _");
    }

    [Test]
    public void TheCueAlwaysLeavesSomethingToRecall()
    {
        MeaningToWordCuedTypeExerciseDefinition.RevealedLetters("chat", 0).Should().Be(2);
        MeaningToWordCuedTypeExerciseDefinition.RevealedLetters("vie", 0).Should().Be(1);
        MeaningToWordCuedTypeExerciseDefinition.RevealedLetters("ab", 0).Should().Be(1);
    }

    // --- cloze with options ------------------------------------------------

    [Test]
    public void TheClozeChoiceBlanksTheWordAndOffersWords()
    {
        var definition = new ContextToWordChoiceExerciseDefinition(Options, Grades, Seeded);

        var payload = definition.Build(Material(), new ExerciseBuildContext(Distractors()));

        payload.Prompt.Should().Be("Screens are _____ now.");
        payload.Options.Should().BeEquivalentTo(new[] { "ubiquitous", "alpha", "beta", "gamma" });
        payload.Transcription.Should().BeNull("the pronunciation belongs to the missing word");
        definition.Resolve(new ExerciseAnswer("ubiquitous", ElapsedMs: 1000), Material()).Should().Be(ReviewGrade.Good);
    }

    [Test]
    public void TheClozeChoiceNeedsASentence()
    {
        new ContextToWordChoiceExerciseDefinition(Options, Grades, Seeded)
            .CanBuild(Material(sentence: null), Distractors()).Should().BeFalse();
    }

    // --- catalogue ---------------------------------------------------------

    [Test]
    public void EveryExerciseOnTheLadderHasARegisteredDefinition()
    {
        var catalog = new ExerciseCatalog(AllDefinitions());
        var onTheLadder = new StudyOptions().EffectiveLadder.SelectMany(level => level.Exercises).ToList();

        onTheLadder.Should().NotBeEmpty();

        foreach (var exercise in onTheLadder)
        {
            catalog.Get(exercise.Type).Should().NotBeNull();
        }
    }

    [Test]
    public void EveryExerciseTypeHasADefinition()
    {
        var catalog = new ExerciseCatalog(AllDefinitions());

        foreach (var type in Enum.GetValues<ExerciseType>().Except(ExerciseCatalog.Retired))
        {
            catalog.Get(type).Type.Should().Be(type);
        }
    }

    [Test]
    public void OnlyTheFollowUpExercisesAreExcludedFromBeingAProbe()
    {
        AllDefinitions().Where(d => !d.CanBeProbe).Select(d => d.Type)
            .Should().Equal(ExerciseType.MeaningToWordPartialLetters, ExerciseType.WordToConnectionsReveal);
    }

    private static List<IExerciseDefinition> AllDefinitions() => new()
    {
        new WordToMeaningRevealExerciseDefinition(),
        new WordToMeaningChoiceExerciseDefinition(Options, Grades, Seeded),
        new MeaningToWordChoiceExerciseDefinition(Options, Grades, Seeded),
        new ContextToWordRecallExerciseDefinition(),
        new MeaningToWordScrambleExerciseDefinition(Options, Grades, Seeded),
        new MeaningToWordRecallExerciseDefinition(),
        new MeaningToWordPartialLettersExerciseDefinition(),
        new MeaningToWordTypeExerciseDefinition(Grades),
        new MeaningToWordCuedTypeExerciseDefinition(Grades),
        new ContextToWordChoiceExerciseDefinition(Options, Grades, Seeded),
        new TranslationToSentenceScrambleExerciseDefinition(Grades, Seeded),
        new WordToConnectionsRevealExerciseDefinition(),
        new WordToSpellingCopyExerciseDefinition(),
        new WordToSpellingCoverExerciseDefinition()
    };
}
