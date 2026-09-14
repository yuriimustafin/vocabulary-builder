using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
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
/// The queue against a real SQLite database.
///
/// These run the actual query rather than a substitute, because two of the bugs they cover
/// were translation failures that only appear against SQLite and would pass silently
/// against an in-memory provider.
/// </summary>
public class StudyQueueTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private StudyTestContext _db = null!;
    private FakeTimeProvider _clock = null!;
    private StudyOptions _options = null!;
    private StudyEnrichmentQueue _enrichment = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new StudyTestContext();
        _clock = new FakeTimeProvider(Start);
        _options = new StudyOptions();
        _enrichment = new StudyEnrichmentQueue();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private GetStudyQueueQueryHandler Handler()
    {
        var grades = new GradeResolver(_options);
        var random = new Random(5);
        var ladder = new ConfiguredExerciseLadder(_options);

        var catalog = new ExerciseCatalog(new IExerciseDefinition[]
        {
            new WordToMeaningRevealExerciseDefinition(),
            new WordToMeaningChoiceExerciseDefinition(_options, grades, random),
            new MeaningToWordChoiceExerciseDefinition(_options, grades, random),
            new ContextToWordRecallExerciseDefinition(),
            new MeaningToWordScrambleExerciseDefinition(_options, grades, random),
            new MeaningToWordRecallExerciseDefinition(),
            new MeaningToWordPartialLettersExerciseDefinition()
        });

        return new GetStudyQueueQueryHandler(
            _db.Context, ladder, catalog, new StudyMaterialResolver(),
            new DistractorPicker(_options, random), new DistractorSource(_db.Context),
            new CardDifficultyCalculator(_options), _enrichment, _options, _clock);
    }

    private Task<StudyQueueDto> Queue() =>
        Handler().Handle(new GetStudyQueueQuery(Language.English), CancellationToken.None);

    /// <summary>Words as the API creates them: no senses, so their content has to be generated.</summary>
    private async Task SeedWords(
        int count, bool withGeneratedContent = true, bool marked = false, string prefix = "word")
    {
        for (var i = 0; i < count; i++)
        {
            var word = new Word
            {
                Headword = $"{prefix}{i:D2}",
                PartOfSpeech = "adjective",
                Language = Language.English,
                Frequency = 1000 + i,
                IsMarkedForStudy = marked
            };
            _db.Context.Words.Add(word);
            await _db.Context.SaveChangesAsync(CancellationToken.None);

            if (withGeneratedContent)
            {
                _db.Context.WordStudyContents.Add(new WordStudyContent
                {
                    WordId = word.Id,
                    Status = StudyContentStatus.Ready,
                    GeneratedDefinition = $"the meaning of {prefix}{i:D2}",
                    GeneratedContextSentence = $"A line using {prefix}{i:D2} once."
                });
            }
        }

        await _db.Context.SaveChangesAsync(CancellationToken.None);
    }

    [Test]
    public async Task TheQueueRunsAgainstSqliteAndReturnsRenderedCards()
    {
        // Regression: ordering the marked bucket by LastModified threw at runtime, because
        // SQLite cannot sort a DateTimeOffset. Nothing but a real query catches that.
        await SeedWords(5);

        var queue = await Queue();

        queue.Cards.Should().NotBeEmpty();
        queue.Cards.Should().OnlyContain(c => c.Exercise != null && c.Exercise.Prompt != null);
    }

    [Test]
    public async Task ARefreshDoesNotIntroduceASecondDayOfNewWords()
    {
        await SeedWords(40);

        for (var refresh = 0; refresh < 5; refresh++)
        {
            await Queue();
        }

        var introduced = await _db.Context.ReviewCards.CountAsync();
        introduced.Should().Be(_options.NewCardsPerDay);
    }

    [Test]
    public async Task TheCapLetsMoreThroughOnceTheDayRollsOver()
    {
        await SeedWords(40);
        await DrainNewWords();

        (await _db.Context.ReviewCards.CountAsync()).Should().Be(_options.NewCardsPerDay);

        _clock.Advance(TimeSpan.FromDays(1));
        await DrainNewWords();

        (await _db.Context.ReviewCards.CountAsync()).Should().Be(_options.NewCardsPerDay * 2);
    }

    [Test]
    public async Task ABatchIntroducesOnlyAFewWordsAtATime()
    {
        // Small batches are what let the first tests fall due while the next handful is
        // still being introduced, so the two end up mixed rather than in blocks.
        await SeedWords(40);

        var queue = await Queue();

        queue.Cards.Should().HaveCount(_options.NewCardsPerBatch);
    }

    [Test]
    public async Task NewWordsComeBackFlaggedAsIntroductions()
    {
        await SeedWords(5);

        var queue = await Queue();

        queue.Cards.Should().OnlyContain(c => c.IsIntroduction,
            "a word being met for the first time has nothing to grade yet");
    }

    [Test]
    public async Task AStepDueShortlyIsPulledForwardWhenNothingElseIsLeft()
    {
        // Twelve words introduced leaves twelve cards due a minute from now and nothing due
        // this instant. Without pulling one forward the first day would stop dead.
        await SeedWords(3);
        await DrainNewWords();

        foreach (var card in await _db.Context.ReviewCards.ToListAsync())
        {
            card.State = CardState.Learning;
            card.DueAtUtc = Start.UtcDateTime.AddMinutes(5);
        }
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        (await Queue()).Cards.Should().NotBeEmpty();
    }

    [Test]
    public async Task AReviewIsNotPulledForward()
    {
        // Bringing a day-scale card days early would be a distortion, not a convenience.
        await SeedWords(3);
        await DrainNewWords();

        foreach (var card in await _db.Context.ReviewCards.ToListAsync())
        {
            card.State = CardState.Review;
            card.IntervalDays = 10;
            card.DueAtUtc = Start.UtcDateTime.AddMinutes(5);
        }
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        (await Queue()).Cards.Should().BeEmpty();
    }

    [Test]
    public async Task TheSessionSaysWhenTheNextWordIsDue()
    {
        await SeedWords(2);
        await DrainNewWords();

        foreach (var card in await _db.Context.ReviewCards.ToListAsync())
        {
            card.State = CardState.Review;
            card.IntervalDays = 3;
            card.DueAtUtc = Start.UtcDateTime.AddHours(4);
        }
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var queue = await Queue();

        queue.Cards.Should().BeEmpty();
        queue.NextDueAtUtc.Should().NotBeNull("the session should say how long the wait is");
    }

    [Test]
    public async Task ABatchMixesNewWordsInAmongTheOnesComingBack()
    {
        await SeedWords(40);
        await Queue();

        // Acknowledge the first handful and make them due, so the next batch has both an
        // introduction and a card coming back to serve. A card left in New has not been met
        // yet and would still count as an introduction.
        foreach (var card in await _db.Context.ReviewCards.ToListAsync())
        {
            card.State = CardState.Learning;
            card.CurrentRung = 1;
            card.DueAtUtc = Start.UtcDateTime.AddMinutes(-1);
        }
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var queue = await Queue();
        var kinds = queue.Cards.Select(c => c.IsIntroduction).ToList();

        kinds.Should().Contain(true).And.Contain(false);

        // Not all of one then all of the other.
        kinds.Distinct().Count().Should().Be(2);
        var firstNew = kinds.IndexOf(true);
        var lastReview = kinds.LastIndexOf(false);
        firstNew.Should().BeLessThan(lastReview, "the two kinds should be interleaved");
    }

    /// <summary>Works through batches the way a session does, until the day's cap is reached.</summary>
    private async Task DrainNewWords()
    {
        for (var batch = 0; batch < 20; batch++)
        {
            var queue = await Queue();

            if (queue.Cards.All(c => !c.IsIntroduction))
            {
                return;
            }
        }
    }

    [Test]
    public async Task MarkingAWordPutsItInTheNextSessionAndClearsTheMark()
    {
        await SeedWords(30);

        var chosen = await _db.Context.Words.OrderByDescending(w => w.Frequency).FirstAsync();
        chosen.IsMarkedForStudy = true;
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var queue = await Queue();

        queue.Cards.Should().Contain(c => c.WordId == chosen.Id,
            "a marked word should not wait behind the frequency list");

        var reloaded = await _db.Context.Words.AsNoTracking().FirstAsync(w => w.Id == chosen.Id);
        reloaded.IsMarkedForStudy.Should().BeFalse("the mark is spent once the word is introduced");
    }

    [Test]
    public async Task WordsWithNothingToShowAreQueuedForFillingInRatherThanIntroduced()
    {
        await SeedWords(3, withGeneratedContent: false);

        var queue = await Queue();

        queue.Cards.Should().BeEmpty();
        queue.PendingEnrichmentCount.Should().Be(3);
        _enrichment.PendingCount.Should().Be(3);

        // Nothing is written for a word that could not be shown, so it is not silently
        // burned against the day's allowance.
        (await _db.Context.ReviewCards.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task AReadyWordIsStudiedWhileOthersAreStillBeingFilledIn()
    {
        await SeedWords(2, withGeneratedContent: true, prefix: "ready");
        await SeedWords(2, withGeneratedContent: false, prefix: "blank");

        var queue = await Queue();

        queue.Cards.Should().HaveCount(2);
        queue.PendingEnrichmentCount.Should().Be(2);
    }

    [Test]
    public async Task DistractorsFallBackToGeneratedDefinitions()
    {
        // Regression: the pool only read Word.Senses, so a collection built without
        // dictionary data had no usable wrong meanings and every word was silently pinned
        // to the bottom rung forever.
        await SeedWords(8);

        var pool = await new DistractorSource(_db.Context)
            .LoadPoolAsync(Language.English, 100, CancellationToken.None);

        pool.Should().OnlyContain(c => c.Meaning != null);

        var target = new StudyMaterial
        {
            WordId = -1, Headword = "target", PartOfSpeech = "adjective", Meaning = "something else"
        };

        new DistractorPicker(_options, new Random(1)).Pick(target, pool)
            .Should().NotBeNull("multiple choice must be buildable from generated content");
    }

    [Test]
    public async Task ADictionarySenseIsPreferredOverGeneratedContentForDistractors()
    {
        var word = new Word
        {
            Headword = "ubiquitous",
            PartOfSpeech = "adjective",
            Language = Language.English,
            Senses = new List<Sense> { new() { Definition = "found everywhere", Examples = new List<string>() } }
        };
        _db.Context.Words.Add(word);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = word.Id,
            Status = StudyContentStatus.Ready,
            GeneratedDefinition = "a generated definition that should not win"
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var pool = await new DistractorSource(_db.Context)
            .LoadPoolAsync(Language.English, 100, CancellationToken.None);

        pool.Single().Meaning.Should().Be("found everywhere");
    }

    [Test]
    public async Task SuspendedWordsStayOutOfTheQueue()
    {
        await SeedWords(3);
        await Queue();

        foreach (var card in await _db.Context.ReviewCards.ToListAsync())
        {
            card.State = CardState.Suspended;
            card.DueAtUtc = Start.UtcDateTime;
        }
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        (await Queue()).Cards.Should().BeEmpty();
    }

    [Test]
    public async Task OnlyTheChosenLanguageIsStudied()
    {
        await SeedWords(3);

        var french = new Word { Headword = "chien", Language = Language.French, PartOfSpeech = "noun" };
        _db.Context.Words.Add(french);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var queue = await Queue();

        queue.Cards.Should().NotContain(c => c.WordId == french.Id);
    }
}
