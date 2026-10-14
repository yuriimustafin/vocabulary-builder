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
    public async Task AWordAnsweredCleanlyIsRecognisedThenBuiltOnceAndLeaves()
    {
        var seen = await StudyUntilLearned(_ => true);

        seen.Should().Equal(
            ExerciseType.WordToMeaningReveal,  // met
            ExerciseType.WordToSpellingCopy,   // recognition: its spelling copied out, then the word
            ExerciseType.MeaningToWordChoice,  // picked from its meaning, then from a sentence
            ExerciseType.ContextToWordChoice,
            ExerciseType.WordToSpellingCover); // scaffolded: looked at, covered, written - that is today

        var card = await Card();
        card.State.Should().Be(CardState.Review);
        card.IntervalDays.Should().Be(1);
        card.PhaseRetrievals.Should().Be(0, "the count belongs to the learning phase that has ended");
        (await GradedReviews()).Should().Be(4);

        // It keeps its place, so the next review carries on up the scaffolded level
        card.CurrentRung.Should().Be(2);
        card.RungStreak.Should().Be(1);
    }

    [Test]
    public async Task AWordThatSlipsGetsMorePracticeAndMeetsTheLevelBelowRatherThanTheSameExercise()
    {
        var missed = false;

        var seen = await StudyUntilLearned(card =>
        {
            // The first time it is picked for a sentence, it is missed - a miss that costs, unlike
            // one on the tolerant exercises of the next level.
            if (card.Exercise.Type == ExerciseType.ContextToWordChoice && !missed)
            {
                missed = true;
                return false;
            }

            return true;
        });

        var missAt = seen.IndexOf(ExerciseType.ContextToWordChoice);
        new ConfiguredExerciseLadder(_options).TypesAt(0).Should().Contain(seen[missAt + 1], "a miss drops one level");
        seen.Count.Should().BeGreaterThan(5, "the slip is paid for in more practice, not a shorter day");
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

        seen.Should().HaveCount(5);
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
    public async Task ALapsedWordRelearnsWithOneCleanAnswerOnTheLevelBelow()
    {
        await SeedCard(rung: 3, streak: 4, CardState.Review, interval: 10, last: ExerciseType.ContextToWordRecall);

        var lapse = await Submit(await Next(), answer: "nothing like it");
        lapse.State.Should().Be(CardState.Relearning);
        lapse.Rung.Should().Be(2);

        var seen = await StudyUntilLearned(_ => true);

        // It was known once: it does not climb back to production before it may leave. A
        // real session had one word take ten answers doing that and still not get out.
        seen.Should().Equal(ExerciseType.WordToSpellingCover);
        (await Card()).State.Should().Be(CardState.Review);
        (await Card()).CurrentRung.Should().Be(2);
    }

    // --- typed answers -------------------------------------------------------------

    /// <summary>On the scaffolded level with two clean answers, so it is next asked to type the word.</summary>
    private Task SeedTypingCard() =>
        SeedCard(rung: 2, streak: 2, CardState.Learning, last: ExerciseType.MeaningToWordScramble);

    [Test]
    public async Task ATypedSlipIsAcceptedButHoldsTheWordAndSaysWhy()
    {
        await SeedTypingCard();

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.MeaningToWordCuedType);

        var result = await Submit(card, answer: "rememebr");

        result.Grade.Should().Be(ReviewGrade.Hard);
        result.Feedback!.Correct.Should().BeTrue();
        result.Feedback.Note.Should().Contain("one letter");
        (await Card()).RungStreak.Should().Be(2, "a near miss neither counts nor costs");
        result.State.Should().Be(CardState.Learning);
    }

    [Test]
    public async Task ATypedSlipThatSpellsAnotherWordInTheCollectionIsWrong()
    {
        await Seed("remembers", "keeps in mind", null);
        await SeedTypingCard();

        var result = await Submit(await Next(), answer: "remembers");

        result.Grade.Should().Be(ReviewGrade.Again);
        result.Feedback!.Correct.Should().BeFalse();
        result.Feedback.Chosen!.Text.Should().Be("remembers");
    }

    [Test]
    public async Task AnExactTypedAnswerCountsInFull()
    {
        await SeedTypingCard();

        var result = await Submit(await Next(), answer: "Remember");

        result.Grade.Should().Be(ReviewGrade.Good);
        result.Feedback!.Note.Should().BeNull();
        (await Card()).CurrentRung.Should().Be(3, "the third clean answer on the level moves it up");
    }

    /// <summary>
    /// The review history keeps the answer as typed and how it was marked - what a slip was,
    /// which nothing else records once the card has moved on.
    /// </summary>
    [Test]
    public async Task TheReviewLogKeepsWhatWasTypedAndHowItWasMarked()
    {
        await SeedTypingCard();

        await Submit(await Next(), answer: "remembr");

        var card = await Card();
        var log = await _db.Context.ReviewLogs.AsNoTracking().OrderBy(l => l.Id).LastAsync();

        log.Answer.Should().Be("remembr");
        log.AnswerMatch.Should().Be("Typo");
        log.Grade.Should().Be(ReviewGrade.Hard);
        log.WordId.Should().Be(card.WordId);
        log.RungAfter.Should().Be(card.CurrentRung);
        log.Tolerated.Should().BeFalse();
    }

    [Test]
    public async Task TheReviewLogNamesTheSentenceItWasAskedOn()
    {
        var first = await AddExample(Word, "I remember her name.", "remember");
        await AddExample(Word, "Remember to call me.", "Remember");
        await SeedCard(rung: 3, streak: 0, CardState.Review, interval: 3, last: ExerciseType.MeaningToWordRecall);

        var card = await Next();
        card.Exercise.ExampleId.Should().Be(first.Id);

        await Submit(card, selfGrade: ReviewGrade.Good);

        var log = await _db.Context.ReviewLogs.AsNoTracking().OrderBy(l => l.Id).LastAsync();
        log.StudyExampleId.Should().Be(first.Id);
        log.Answer.Should().BeNull("a self-graded recall has no answer to keep");
    }

    [Test]
    public async Task TheLastExerciseAskedIsRememberedOnTheCard()
    {
        var card = await Next();
        await Answer(card, correct: true);

        (await Card()).LastExerciseType.Should().Be(ExerciseType.WordToMeaningReveal);

        card = await Next();
        await Answer(card, correct: true);

        (await Card()).LastExerciseType.Should().Be(ExerciseType.WordToSpellingCopy);
    }

    // --- example sentences -----------------------------------------------------------

    private async Task<StudyExample> AddExample(string headword, string sentence, string form, string? translation = null)
    {
        var word = await _db.Context.Words.SingleAsync(w => w.Headword == headword);
        var example = new StudyExample { WordId = word.Id, Sentence = sentence, Form = form, Translation = translation };

        _db.Context.StudyExamples.Add(example);
        await _db.Context.SaveChangesAsync(CancellationToken.None);
        return example;
    }

    private async Task<StudyExample> Reload(StudyExample example)
    {
        _db.Context.ChangeTracker.Clear();
        return await _db.Context.StudyExamples.AsNoTracking().SingleAsync(e => e.Id == example.Id);
    }

    [Test]
    public async Task AClozeAnsweredCorrectlyMarksItsSentenceAsPractised()
    {
        var first = await AddExample(Word, "I remember her name.", "remember");
        var second = await AddExample(Word, "Remember to call me.", "Remember");
        await SeedCard(rung: 3, streak: 0, CardState.Review, interval: 3, last: ExerciseType.MeaningToWordRecall);

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.ContextToWordRecall);
        card.Exercise.ExampleId.Should().Be(first.Id);
        card.Exercise.Prompt.Should().Be("I _____ her name.");

        var result = await Submit(card, selfGrade: ReviewGrade.Good);

        (await Reload(first)).Successes.Should().Be(1);
        (await Reload(first)).LastUsedAtUtc.Should().NotBeNull();

        // Next time round it is the sentence not yet practised that is asked
        var word = await _db.Context.Words.Include(w => w.Senses).AsNoTracking().SingleAsync(w => w.Headword == Word);
        var examples = await _db.Context.StudyExamples.AsNoTracking().Where(e => e.WordId == word.Id).ToListAsync();
        new StudyMaterialResolver().Resolve(word, null, new StudyExampleSet(examples, Array.Empty<string>()))
            .ExampleId.Should().Be(second.Id);
    }

    [Test]
    public async Task AMissedClozeKeepsItsSentenceAtTheFront()
    {
        var first = await AddExample(Word, "I remember her name.", "remember");
        await AddExample(Word, "Remember to call me.", "Remember");
        await SeedCard(rung: 3, streak: 0, CardState.Review, interval: 3, last: ExerciseType.MeaningToWordRecall);

        await Submit(await Next(), selfGrade: ReviewGrade.Again);

        var reloaded = await Reload(first);
        reloaded.Successes.Should().Be(0);
        reloaded.LastUsedAtUtc.Should().NotBeNull();
    }

    [Test]
    public async Task AnExampleIdBelongingToAnotherWordIsIgnored()
    {
        var other = await AddExample("window", "Open the window now.", "window");
        await SeedCard(rung: 3, streak: 0, CardState.Review, interval: 3, last: ExerciseType.MeaningToWordRecall);
        var card = await Next();

        _db.Context.ChangeTracker.Clear();
        await ReviewHandler().Handle(
            new SubmitReviewCommand
            {
                CardId = card.CardId,
                AttemptId = Guid.NewGuid(),
                ExerciseType = card.Exercise.Type,
                SelfGrade = ReviewGrade.Good,
                ExampleId = other.Id,
                ElapsedMs = 5000
            },
            CancellationToken.None);

        (await Reload(other)).Successes.Should().Be(0);
    }

    [Test]
    public async Task TheFeedbackCarriesTheWordsConnections()
    {
        var word = await _db.Context.Words.SingleAsync(w => w.Headword == Word);
        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = word.Id,
            Status = StudyContentStatus.Ready,
            Etymology = "From Latin rememorari, to call to mind again.",
            Mnemonic = "Sounds like 'ream member'."
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);
        await SeedTypingCard();

        var card = await Next();
        card.Exercise.Connections!.Etymology.Should().StartWith("From Latin");

        var result = await Submit(card, answer: "remember");
        result.Feedback!.Connections!.Mnemonic.Should().Be("Sounds like 'ream member'.");
    }

    // --- mistake-tolerant exercises and connections ---------------------------------

    private async Task AddContent()
    {
        var word = await _db.Context.Words.SingleAsync(w => w.Headword == Word);
        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = word.Id,
            Status = StudyContentStatus.Ready,
            PromptVersion = VocabularyBuilder.Application.Study.Enrichment.StudyContentPrompt.Version,
            Mnemonic = "Sounds like 'ream member'.",
            Etymology = "From Latin rememorari."
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);
    }

    [Test]
    public async Task AMissOnAMistakeTolerantExerciseCostsTheWordNothing()
    {
        await AddContent();
        await SeedCard(rung: 2, streak: 1, CardState.Review, interval: 5, last: ExerciseType.WordToSpellingCover);
        var before = await Card();

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.MeaningToWordScramble);

        var result = await Submit(card, answer: "rebmemer");

        result.Grade.Should().Be(ReviewGrade.Again);
        result.Tolerated.Should().BeTrue();
        result.Feedback!.Note.Should().Contain("does not count against");
        result.FollowUps.Select(f => f.Exercise.Type).Should().Equal(ExerciseType.WordToConnectionsReveal);

        var after = await Card();
        after.CurrentRung.Should().Be(2);
        after.RungStreak.Should().Be(1);
        after.State.Should().Be(CardState.Review);
        after.IntervalDays.Should().Be(before.IntervalDays);
        after.EaseFactor.Should().Be(before.EaseFactor);
        after.Lapses.Should().Be(0);
        after.RecentSuccessRate.Should().Be(before.RecentSuccessRate);
        after.DueAtUtc.Should().Be(Now.AddMinutes(1), "it comes back shortly");

        // ...to be asked another way
        (await Next()).Exercise.Type.Should().NotBe(ExerciseType.MeaningToWordScramble);
    }

    [Test]
    public async Task ASuccessOnAMistakeTolerantExerciseCountsAsUsual()
    {
        await AddContent();
        await SeedCard(rung: 2, streak: 1, CardState.Review, interval: 5, last: ExerciseType.WordToSpellingCover);

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.MeaningToWordScramble);

        var result = await Submit(card, answer: Word);

        result.Tolerated.Should().BeFalse();
        (await Card()).RungStreak.Should().Be(2, "a clean success on it counts like any other");
    }

    [Test]
    public async Task RebuildingASentenceCountsAsPractisingIt()
    {
        var example = await AddExample(Word, "I remember her name well.", "remember", "Я добре пам'ятаю її ім'я.");
        await AddExample(Word, "Remember to call me.", "Remember");

        // A review: a sentence is not rebuilt on the day a word is first learned
        await SeedCard(rung: 2, streak: 2, CardState.Review, interval: 3, last: ExerciseType.MeaningToWordScramble);

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.TranslationToSentenceScramble);
        card.Exercise.ExampleId.Should().Be(example.Id);
        card.Exercise.SentenceStart.Should().Be("I ");
        card.Exercise.SentenceEnd.Should().Be(" well.");

        var result = await Submit(card, answer: "remember her name");

        result.Grade.Should().Be(ReviewGrade.Good);
        (await Reload(example)).Successes.Should().Be(1);
    }

    [Test]
    public async Task AMissPickingTheWordIsFollowedByItsConnectionsAlone()
    {
        // Not knowing which word it was is not a spelling problem: no letters to walk through
        await AddContent();
        await SeedCard(rung: 1, streak: 1, CardState.Review, interval: 5, last: ExerciseType.WordToSpellingCopy);

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.MeaningToWordChoice);

        var result = await Submit(card, answer: card.Exercise.Options!.First(o => o != Word));

        result.Tolerated.Should().BeFalse();
        result.State.Should().Be(CardState.Relearning, "an ordinary miss is a lapse");
        result.FollowUps.Select(f => f.Exercise.Type).Should().Equal(ExerciseType.WordToConnectionsReveal);
        result.FollowUps[0].Exercise.Connections!.Mnemonic.Should().Be("Sounds like 'ream member'.");
    }

    [Test]
    public async Task AMissProducingTheWordIsFollowedByItsConnectionsThenShrinkingCues()
    {
        await AddContent();
        await SeedTypingCard();

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.MeaningToWordCuedType);

        var result = await Submit(card, answer: "nothing like it");

        result.FollowUps[0].Exercise.Type.Should().Be(ExerciseType.WordToConnectionsReveal);
        result.FollowUps.Skip(1).Select(f => f.Exercise.Type).Should().Contain(ExerciseType.MeaningToWordPartialLetters);
    }

    [Test]
    public async Task ARightAnswerWhileLearningGetsNoReExposure()
    {
        // It is back within minutes anyway. In a real first session these were half of all
        // the cards shown.
        await SeedCard(rung: 1, streak: 1, CardState.Learning, last: ExerciseType.WordToSpellingCopy);

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.MeaningToWordChoice);

        var result = await Submit(card, answer: Word, elapsedMs: 20_000);

        result.Grade.Should().Be(ReviewGrade.Hard, "slow enough to be shaky, which used to bring follow-ups");
        result.State.Should().Be(CardState.Learning);
        result.FollowUps.Should().BeEmpty();
    }

    [Test]
    public async Task TheMissingWordHintIsFree()
    {
        // A translation to tell apart words that all fit the sentence: taking it does not hold the word
        await SeedCard(rung: 1, streak: 1, CardState.Learning, last: ExerciseType.MeaningToWordChoice);

        var card = await Next();
        card.Exercise.Type.Should().Be(ExerciseType.ContextToWordChoice);
        card.Exercise.Hint.Should().Be(Meaning);

        _db.Context.ChangeTracker.Clear();
        var result = await ReviewHandler().Handle(
            new SubmitReviewCommand
            {
                CardId = card.CardId,
                AttemptId = card.AttemptId,
                ExerciseType = card.Exercise.Type,
                Answer = Word,
                // Slow, as reading the hint makes it: that is not held against the answer either
                ElapsedMs = 20_000,
                HintUsed = true,
                ExampleId = card.Exercise.ExampleId
            },
            CancellationToken.None);

        result.Grade.Should().Be(ReviewGrade.Good);
        (await Card()).RungStreak.Should().Be(2, "the hint was free, so the success counts");

        // ...and the log still says it was taken, so a session can be read back
        var log = await _db.Context.ReviewLogs.AsNoTracking().OrderBy(l => l.Id).LastAsync();
        log.HintUsed.Should().BeTrue();
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
                ElapsedMs = elapsedMs,
                // As the page does: the sentence the exercise was asked on goes back with it
                ExampleId = card.Exercise.ExampleId
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
            new MeaningToWordRecallExerciseDefinition(),
            new MeaningToWordPartialLettersExerciseDefinition(),
            new MeaningToWordTypeExerciseDefinition(grades),
            new MeaningToWordCuedTypeExerciseDefinition(grades),
            new TranslationToSentenceScrambleExerciseDefinition(grades, random),
            new WordToConnectionsRevealExerciseDefinition(),
            new WordToSpellingCopyExerciseDefinition(),
            new WordToSpellingCoverExerciseDefinition()
        });
    }
}
