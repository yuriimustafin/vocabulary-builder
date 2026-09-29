using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Commands;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Exercises.Definitions;
using VocabularyBuilder.Application.Study.Queries;
using VocabularyBuilder.Application.Study.Scheduling;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.UnitTests.Study;

/// <summary>
/// Whole learning sessions, through the real queue, introduction and review handlers
/// against SQLite: which exercises a word meets, how long it stays, and what moves it.
/// </summary>
public class LearningSessionTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private const string Word = "remember";
    private const string Meaning = "to bring back to mind";

    private StudyTestContext _db = null!;
    private FakeTimeProvider _clock = null!;
    private StudyOptions _options = null!;

    [SetUp]
    public async Task SetUp()
    {
        _db = new StudyTestContext();
        _clock = new FakeTimeProvider(Start);
        // Every word introduced in the first batch, so the one under test is always among them.
        _options = new StudyOptions { IntervalFuzzPercent = 0, NewCardsPerBatch = 10 };

        await Seed(Word, Meaning, "I remember the day we met.");

        // Enough other words for multiple choice to have its distractors.
        await Seed("window", "an opening in a wall", "Open the window.");
        await Seed("pocket", "a small bag sewn into clothing", "It is in my pocket.");
        await Seed("little", "small in size", "A little dog barked.");
        await Seed("wander", "to walk without aim", "We wander the streets.");
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // --- the sessions ----------------------------------------------------------

    [Test]
    public async Task AWordAnsweredCleanlyClimbsThroughEachLevelAndLeavesAfterTwoProductions()
    {
        var seen = await StudyUntilLearned(_ => true);

        seen.Should().Equal(
            ExerciseType.WordToMeaningReveal,           // met
            ExerciseType.MeaningToWordChoice,           // recognition: one success
            ExerciseType.MeaningToWordSyllableScramble, // scaffolded: support fading over three
            ExerciseType.MeaningToWordScramble,
            ExerciseType.MeaningToWordCuedType,
            ExerciseType.ContextToWordRecall,           // production: two, spaced
            ExerciseType.MeaningToWordType);

        var card = await Card();
        card.State.Should().Be(CardState.Review);
        card.IntervalDays.Should().Be(1);
        card.PhaseRetrievals.Should().Be(0, "the count belongs to the learning phase that has ended");
        (await GradedReviews()).Should().Be(6);
    }

    [Test]
    public async Task AWordThatSlipsGetsMorePracticeAndMeetsTheLevelBelowRatherThanTheSameExercise()
    {
        var missed = false;

        var seen = await StudyUntilLearned(card =>
        {
            // The first time it reaches production, it is missed.
            if (card.Exercise.Type == ExerciseType.ContextToWordRecall && !missed)
            {
                missed = true;
                return false;
            }

            return true;
        });

        var missAt = seen.IndexOf(ExerciseType.ContextToWordRecall);
        seen[missAt + 1].Should().Be(ExerciseType.MeaningToWordSyllableScramble, "a miss drops one level");
        seen.Count.Should().BeGreaterThan(7, "the slip is paid for in more practice, not a shorter day");
        (await Card()).State.Should().Be(CardState.Review);
    }

    [Test]
    public async Task NoExerciseIsAskedTwiceRunning()
    {
        var misses = 0;

        var seen = await StudyUntilLearned(card => card.Exercise.Type is not ExerciseType.MeaningToWordChoice || misses++ > 0);

        seen.Zip(seen.Skip(1)).Should().NotContain(pair => pair.First == pair.Second);
    }

    [Test]
    public async Task AQuickCorrectPickNeverShortensTheFirstDay()
    {
        // Regression: a multiple-choice pick under three seconds was Easy, which sent a word
        // met a minute earlier four days away.
        var seen = await StudyUntilLearned(_ => true, elapsedMs: 800);

        seen.Should().HaveCount(7);
        (await Card()).State.Should().Be(CardState.Review);
    }

    [Test]
    public async Task AWordThatKeepsSlippingStillLeavesTheSessionEventually()
    {
        // Every production attempt is missed until the cap is reached, then answered.
        var seen = await StudyUntilLearned(card =>
            card.Exercise.Type is not (ExerciseType.ContextToWordRecall or ExerciseType.MeaningToWordType
                or ExerciseType.MeaningToWordRecall));

        seen.Count.Should().BeLessThanOrEqualTo(_options.MaxLearningRetrievals + 2);
        (await Card()).State.Should().Be(CardState.Review);
    }

    [Test]
    public async Task ALapsedWordRelearnsFromTheLevelBelowAndNeedsOneProduction()
    {
        await SeedCard(rung: 3, streak: 4, CardState.Review, interval: 10, last: ExerciseType.ContextToWordRecall);

        var lapse = await Submit(await Next(), answer: "nothing like it");
        lapse.State.Should().Be(CardState.Relearning);
        lapse.Rung.Should().Be(2);

        var seen = await StudyUntilLearned(_ => true);

        seen.Should().Equal(
            ExerciseType.MeaningToWordSyllableScramble,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.MeaningToWordCuedType,
            ExerciseType.ContextToWordRecall);
        (await Card()).State.Should().Be(CardState.Review);
    }

    // --- typed answers -------------------------------------------------------------

    [Test]
    public async Task ATypedSlipIsAcceptedButHoldsTheWordAndSaysWhy()
    {
        await SeedCard(rung: 3, streak: 1, CardState.Learning, last: ExerciseType.ContextToWordRecall);

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.MeaningToWordType);

        var result = await Submit(card, answer: "rememebr");

        result.Grade.Should().Be(ReviewGrade.Hard);
        result.Feedback!.Correct.Should().BeTrue();
        result.Feedback.Note.Should().Contain("one letter");
        (await Card()).RungStreak.Should().Be(1, "a near miss neither counts nor costs");
        result.State.Should().Be(CardState.Learning);
    }

    [Test]
    public async Task ATypedSlipThatSpellsAnotherWordInTheCollectionIsWrong()
    {
        await Seed("remembers", "keeps in mind", null);
        await SeedCard(rung: 3, streak: 1, CardState.Learning, last: ExerciseType.ContextToWordRecall);

        var result = await Submit(await Next(), answer: "remembers");

        result.Grade.Should().Be(ReviewGrade.Again);
        result.Feedback!.Correct.Should().BeFalse();
        result.Feedback.Chosen!.Text.Should().Be("remembers");
    }

    [Test]
    public async Task AnExactTypedAnswerCountsInFull()
    {
        await SeedCard(rung: 3, streak: 1, CardState.Learning, last: ExerciseType.ContextToWordRecall);

        var result = await Submit(await Next(), answer: "Remember");

        result.Grade.Should().Be(ReviewGrade.Good);
        result.Feedback!.Note.Should().BeNull();
        (await Card()).RungStreak.Should().Be(2);
    }

    [Test]
    public async Task TheLastExerciseAskedIsRememberedOnTheCard()
    {
        var card = await Next();
        await Answer(card, correct: true);

        (await Card()).LastExerciseType.Should().Be(ExerciseType.WordToMeaningReveal);

        card = await Next();
        await Answer(card, correct: true);

        (await Card()).LastExerciseType.Should().Be(ExerciseType.MeaningToWordChoice);
    }

    // --- driving a session ---------------------------------------------------------

    /// <summary>
    /// Serves the word and answers it until it leaves learning, returning the exercises it
    /// met. The clock moves to each learning step's due time rather than waiting it out.
    /// </summary>
    private async Task<List<ExerciseType>> StudyUntilLearned(Func<StudyCardDto, bool> answerCorrectly, int elapsedMs = 5000)
    {
        var seen = new List<ExerciseType>();

        for (var i = 0; i < 20; i++)
        {
            var card = await Next();
            seen.Add(card.Exercise.Type);

            await Answer(card, answerCorrectly(card), elapsedMs);

            if ((await Card()).State == CardState.Review)
            {
                return seen;
            }
        }

        throw new AssertionException($"The word never left learning: {string.Join(", ", seen)}");
    }

    private async Task<StudyCardDto> Next()
    {
        var existing = await _db.Context.ReviewCards.AsNoTracking().FirstOrDefaultAsync(c => c.Word.Headword == Word);

        if (existing?.DueAtUtc is { } due && due > Now)
        {
            _clock.Advance(due - Now + TimeSpan.FromSeconds(1));
        }

        _db.Context.ChangeTracker.Clear();

        var queue = await QueueHandler().Handle(new GetStudyQueueQuery(Language.English), CancellationToken.None);
        var card = queue.Cards.FirstOrDefault(c => c.Headword == Word);

        card.Should().NotBeNull($"{Word} should be due at {Now:HH:mm:ss}");
        return card!;
    }

    private async Task<object> Answer(StudyCardDto card, bool correct, int elapsedMs = 5000)
    {
        if (card.IsIntroduction)
        {
            return await IntroductionHandler().Handle(
                new AcknowledgeIntroductionCommand
                {
                    CardId = card.CardId,
                    AttemptId = card.AttemptId,
                    ExerciseType = card.Exercise.Type
                },
                CancellationToken.None);
        }

        var exercise = card.Exercise;

        if (exercise.GradingMode == GradingMode.SelfReported)
        {
            return await Submit(card, selfGrade: correct ? ReviewGrade.Good : ReviewGrade.Again, elapsedMs: elapsedMs);
        }

        var answer = exercise.Type switch
        {
            ExerciseType.WordToMeaningChoice => correct ? Meaning : exercise.Options!.First(o => o != Meaning),
            ExerciseType.MeaningToWordChoice or ExerciseType.ContextToWordChoice =>
                correct ? Word : exercise.Options!.First(o => o != Word),
            _ => correct ? Word : "nothing like it"
        };

        return await Submit(card, answer: answer, elapsedMs: elapsedMs);
    }

    private Task<ReviewResultDto> Submit(
        StudyCardDto card, string? answer = null, ReviewGrade? selfGrade = null, int elapsedMs = 5000)
    {
        _db.Context.ChangeTracker.Clear();

        return ReviewHandler().Handle(
            new SubmitReviewCommand
            {
                CardId = card.CardId,
                AttemptId = card.AttemptId,
                ExerciseType = card.Exercise.Type,
                Answer = answer,
                SelfGrade = selfGrade,
                ElapsedMs = elapsedMs
            },
            CancellationToken.None);
    }

    private async Task<ReviewCard> Card()
    {
        _db.Context.ChangeTracker.Clear();
        return await _db.Context.ReviewCards.AsNoTracking().SingleAsync(c => c.Word.Headword == Word);
    }

    private async Task<int> GradedReviews()
    {
        var card = await Card();
        return await _db.Context.ReviewLogs.CountAsync(l => l.ReviewCardId == card.Id && !l.IsScaffold);
    }

    // --- set-up --------------------------------------------------------------------

    private async Task Seed(string headword, string definition, string? example)
    {
        _db.Context.Words.Add(new Word
        {
            Headword = headword,
            PartOfSpeech = "verb",
            Language = Language.English,
            Senses = new List<Sense>
            {
                new()
                {
                    Definition = definition,
                    Examples = example is null ? new List<string>() : new List<string> { example }
                }
            }
        });

        await _db.Context.SaveChangesAsync(CancellationToken.None);
    }

    private async Task SeedCard(
        int rung, int streak, CardState state, int interval = 0, ExerciseType? last = null)
    {
        var word = await _db.Context.Words.SingleAsync(w => w.Headword == Word);

        _db.Context.ReviewCards.Add(new ReviewCard
        {
            WordId = word.Id,
            State = state,
            CurrentRung = rung,
            RungStreak = streak,
            LastExerciseType = last,
            IntervalDays = interval,
            LearningStepIndex = state == CardState.Review ? 0 : 3,
            IntroducedAtUtc = Now.AddDays(-20),
            LastReviewedAtUtc = Now.AddMinutes(state == CardState.Review ? -interval * 24 * 60 : -10),
            DueAtUtc = Now
        });

        await _db.Context.SaveChangesAsync(CancellationToken.None);
    }

    private GetStudyQueueQueryHandler QueueHandler()
    {
        var ladder = new ConfiguredExerciseLadder(_options);
        var random = new Random(7);

        return new GetStudyQueueQueryHandler(
            _db.Context, ladder, Catalog(random), new StudyMaterialResolver(),
            new DistractorPicker(_options, random), new DistractorSource(_db.Context),
            new CardDifficultyCalculator(_options), new StudyEnrichmentQueue(), _options, _clock);
    }

    private AcknowledgeIntroductionCommandHandler IntroductionHandler() =>
        new(_db.Context, new Sm2Scheduler(_options, new Random(1)), new ConfiguredExerciseLadder(_options), _clock);

    private SubmitReviewCommandHandler ReviewHandler()
    {
        var ladder = new ConfiguredExerciseLadder(_options);
        var random = new Random(7);

        return new SubmitReviewCommandHandler(
            _db.Context, new Sm2Scheduler(_options, random), new LearningExitCriterion(_options, ladder), ladder,
            Catalog(random), new StudyMaterialResolver(), new DistractorPicker(_options, random),
            new DistractorSource(_db.Context), new CardDifficultyCalculator(_options),
            new ScaffoldSequencer(_options, ladder), new StudyWordLookup(_db.Context), _options, _clock);
    }

    private ExerciseCatalog Catalog(Random random)
    {
        var grades = new GradeResolver(_options);

        return new ExerciseCatalog(new IExerciseDefinition[]
        {
            new WordToMeaningRevealExerciseDefinition(),
            new WordToMeaningChoiceExerciseDefinition(_options, grades, random),
            new MeaningToWordChoiceExerciseDefinition(_options, grades, random),
            new ContextToWordChoiceExerciseDefinition(_options, grades, random),
            new ContextToWordRecallExerciseDefinition(),
            new MeaningToWordScrambleExerciseDefinition(_options, grades, random),
            new MeaningToWordSyllableScrambleExerciseDefinition(_options, grades, random),
            new MeaningToWordRecallExerciseDefinition(),
            new MeaningToWordPartialLettersExerciseDefinition(),
            new MeaningToWordTypeExerciseDefinition(grades),
            new MeaningToWordCuedTypeExerciseDefinition(grades)
        });
    }
}
