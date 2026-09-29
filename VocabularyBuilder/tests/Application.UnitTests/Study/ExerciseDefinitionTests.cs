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

    [Test]
    public void DecoyLettersAreAddedWhenConfiguredAndNeverBelongToTheWord()
    {
        var options = new StudyOptions { ScrambleDecoyLetters = 2 };
        var definition = new MeaningToWordScrambleExerciseDefinition(options, Grades, new Random(11));

        var tiles = definition.Build(Material("apt"), new ExerciseBuildContext()).Tiles!;

        tiles.Should().HaveCount(5);
        tiles.Count(t => t is "a" or "p" or "t").Should().Be(3);
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

        definition.Build(Material("élève"), new ExerciseBuildContext()).Tiles
            .Should().BeEquivalentTo(new[] { "é", "l", "è", "v", "e" });
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

    // --- syllable scramble -------------------------------------------------

    [Test]
    public void TheSyllableScrambleOffersTheWordsSyllablesShuffled()
    {
        var definition = new MeaningToWordSyllableScrambleExerciseDefinition(Options, Grades, new Random(1));

        var payload = definition.Build(Material("chocolat"), new ExerciseBuildContext());

        payload.Tiles.Should().BeEquivalentTo(new[] { "cho", "co", "lat" });
        payload.Tiles.Should().NotEqual(new[] { "cho", "co", "lat" }, "tiles already in order ask nothing");
        payload.GradingMode.Should().Be(GradingMode.Automatic);
    }

    [Test]
    public void AWordOfFewerThanThreeSyllablesIsLeftToTheLetters()
    {
        var definition = new MeaningToWordSyllableScrambleExerciseDefinition(Options, Grades, Seeded);

        definition.CanBuild(Material("window"), null).Should().BeFalse();
        definition.CanBuild(Material("remember"), null).Should().BeTrue();
    }

    [Test]
    public void TheSyllableScrambleIsMarkedLikeTheLetterScramble()
    {
        var definition = new MeaningToWordSyllableScrambleExerciseDefinition(Options, Grades, Seeded);

        definition.Resolve(new ExerciseAnswer("chocolat", ElapsedMs: 5000), Material("chocolat")).Should().Be(ReviewGrade.Good);
        definition.Resolve(new ExerciseAnswer("cocholat", ElapsedMs: 5000), Material("chocolat")).Should().Be(ReviewGrade.Again);
        definition.Resolve(new ExerciseAnswer("chocolat", ElapsedMs: 5000, Resets: 1), Material("chocolat"))
            .Should().Be(ReviewGrade.Hard);
    }

    [Test]
    public void TheSyllableScrambleNeverAddsDecoys()
    {
        // A decoy syllable would be a misspelling of the word, and seeing one makes it
        // more likely to be written later.
        var options = new StudyOptions { ScrambleDecoyLetters = 3 };
        var definition = new MeaningToWordSyllableScrambleExerciseDefinition(options, new GradeResolver(options), Seeded);

        definition.Build(Material("chocolat"), new ExerciseBuildContext()).Tiles.Should().HaveCount(3);
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

        foreach (var type in Enum.GetValues<ExerciseType>())
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
        new MeaningToWordSyllableScrambleExerciseDefinition(Options, Grades, Seeded),
        new MeaningToWordTypeExerciseDefinition(Grades),
        new MeaningToWordCuedTypeExerciseDefinition(Grades),
        new ContextToWordChoiceExerciseDefinition(Options, Grades, Seeded),
        new WordToCollocatesChoiceExerciseDefinition(Grades, Seeded),
        new TranslationToSentenceScrambleExerciseDefinition(Grades, Seeded),
        new WordToConnectionsRevealExerciseDefinition()
    };
}
