using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Commands;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Exercises.Definitions;
using VocabularyBuilder.Application.Study.Scheduling;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.UnitTests.Study;

/// <summary>
/// What comes back after an automatically graded answer.
///
/// A multiple-choice question never reveals its answer to the learner, so unless the result
/// says what the word was, a wrong answer teaches nothing at all. Wrong options are other
/// real words, so the one that was picked can be named rather than merely marked wrong.
/// </summary>
public class ReviewFeedbackTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private StudyTestContext _db = null!;
    private FakeTimeProvider _clock = null!;
    private StudyOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new StudyTestContext();
        _clock = new FakeTimeProvider(Start);
        _options = new StudyOptions();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private SubmitReviewCommandHandler Handler()
    {
        var grades = new GradeResolver(_options);
        var random = new Random(3);
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

        return new SubmitReviewCommandHandler(
            _db.Context, new Sm2Scheduler(_options, random), ladder, catalog,
            new StudyMaterialResolver(), new DistractorPicker(_options, random),
            new DistractorSource(_db.Context), new CardDifficultyCalculator(_options),
            new ScaffoldSequencer(_options, ladder), new StudyWordLookup(_db.Context),
            _options, _clock);
    }

    private async Task<ReviewCard> AddWordWithCard(string headword, string definition, string? example = null)
    {
        var word = new Word
        {
            Headword = headword,
            PartOfSpeech = "adjective",
            Language = Language.English,
            Senses = new List<Sense>
            {
                new()
                {
                    Definition = definition,
                    Examples = example is null ? new List<string>() : new List<string> { example }
                }
            }
        };

        _db.Context.Words.Add(word);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var card = new ReviewCard
        {
            WordId = word.Id,
            State = CardState.Review,
            CurrentRung = 1,
            IntervalDays = 3,
            IntroducedAtUtc = Start.UtcDateTime.AddDays(-1),
            LastReviewedAtUtc = Start.UtcDateTime.AddDays(-1),
            DueAtUtc = Start.UtcDateTime
        };

        _db.Context.ReviewCards.Add(card);
        await _db.Context.SaveChangesAsync(CancellationToken.None);
        return card;
    }

    private Task<ReviewResultDto> Answer(ReviewCard card, ExerciseType type, string answer) =>
        Handler().Handle(
            new SubmitReviewCommand
            {
                CardId = card.Id,
                AttemptId = Guid.NewGuid(),
                ExerciseType = type,
                Answer = answer,
                ElapsedMs = 5000
            },
            CancellationToken.None);

    [Test]
    public async Task ACorrectAnswerStillShowsTheWordAgain()
    {
        var card = await AddWordWithCard("ubiquitous", "found everywhere", "Screens are ubiquitous.");

        var result = await Answer(card, ExerciseType.WordToMeaningChoice, "found everywhere");

        result.Feedback.Should().NotBeNull();
        result.Feedback!.Correct.Should().BeTrue();
        result.Feedback.Headword.Should().Be("ubiquitous");
        result.Feedback.Meaning.Should().Be("found everywhere");
        result.Feedback.ContextSentence.Should().Be("Screens are ubiquitous.");
        result.Feedback.Chosen.Should().BeNull("there is nothing to explain when it was right");
    }

    [Test]
    public async Task ChoosingTheWrongMeaningNamesTheWordItBelongsTo()
    {
        var card = await AddWordWithCard("ubiquitous", "found everywhere");
        await AddWordWithCard("ephemeral", "lasting a very short time");

        var result = await Answer(card, ExerciseType.WordToMeaningChoice, "lasting a very short time");

        result.Feedback!.Correct.Should().BeFalse();
        result.Feedback.Headword.Should().Be("ubiquitous", "the word being asked about is still shown");
        result.Feedback.Chosen.Should().NotBeNull();
        result.Feedback.Chosen!.Text.Should().Be("lasting a very short time");
        result.Feedback.Chosen.Headword.Should().Be("ephemeral", "that meaning belongs to another word");
    }

    [Test]
    public async Task ChoosingTheWrongWordNamesWhatThatWordMeans()
    {
        var card = await AddWordWithCard("ubiquitous", "found everywhere");
        await AddWordWithCard("ephemeral", "lasting a very short time");

        var result = await Answer(card, ExerciseType.MeaningToWordChoice, "ephemeral");

        result.Feedback!.Correct.Should().BeFalse();
        result.Feedback.Headword.Should().Be("ubiquitous");
        result.Feedback.Chosen!.Text.Should().Be("ephemeral");
        result.Feedback.Chosen.Headword.Should().Be("ephemeral");
        result.Feedback.Chosen.Meaning.Should().Be("lasting a very short time");
    }

    [Test]
    public async Task AGeneratedDefinitionCanBeExplainedToo()
    {
        // The distractor pool draws on generated content, so anything it could offer has to
        // be identifiable here as well.
        var card = await AddWordWithCard("ubiquitous", "found everywhere");

        var generated = new Word { Headword = "obscure", PartOfSpeech = "adjective", Language = Language.English };
        _db.Context.Words.Add(generated);
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        _db.Context.WordStudyContents.Add(new WordStudyContent
        {
            WordId = generated.Id,
            Status = StudyContentStatus.Ready,
            GeneratedDefinition = "hard to make out"
        });
        await _db.Context.SaveChangesAsync(CancellationToken.None);

        var result = await Answer(card, ExerciseType.WordToMeaningChoice, "hard to make out");

        result.Feedback!.Chosen!.Headword.Should().Be("obscure");
    }

    [Test]
    public async Task AnUnrecognisedChoiceIsStillReportedAsWhatWasPicked()
    {
        var card = await AddWordWithCard("ubiquitous", "found everywhere");

        var result = await Answer(card, ExerciseType.WordToMeaningChoice, "something not in the collection");

        result.Feedback!.Chosen.Should().NotBeNull();
        result.Feedback.Chosen!.Text.Should().Be("something not in the collection");
        result.Feedback.Chosen.Headword.Should().BeNull();
    }

    [Test]
    public async Task SpellingIsMarkedButHasNoOtherWordToBlame()
    {
        var card = await AddWordWithCard("ubiquitous", "found everywhere");

        var result = await Answer(card, ExerciseType.MeaningToWordScramble, "ubiqutious");

        result.Feedback!.Correct.Should().BeFalse();
        result.Feedback.Headword.Should().Be("ubiquitous");
        result.Feedback.Chosen!.Text.Should().Be("ubiqutious");
        result.Feedback.Chosen.Headword.Should().BeNull("a misspelling is not another word");
    }

    [Test]
    public async Task ASelfGradedExerciseGetsNoFeedback()
    {
        // The learner revealed the answer themselves, so there is nothing left to tell them.
        var card = await AddWordWithCard("ubiquitous", "found everywhere");

        var result = await Handler().Handle(
            new SubmitReviewCommand
            {
                CardId = card.Id,
                AttemptId = Guid.NewGuid(),
                ExerciseType = ExerciseType.WordToMeaningReveal,
                SelfGrade = ReviewGrade.Good,
                ElapsedMs = 4000
            },
            CancellationToken.None);

        result.Feedback.Should().BeNull();
    }

    [Test]
    public async Task AHardButCorrectAnswerIsNotReportedAsWrong()
    {
        var card = await AddWordWithCard("ubiquitous", "found everywhere");

        // Slow enough to be graded Hard, but the answer was right.
        var result = await Handler().Handle(
            new SubmitReviewCommand
            {
                CardId = card.Id,
                AttemptId = Guid.NewGuid(),
                ExerciseType = ExerciseType.WordToMeaningChoice,
                Answer = "found everywhere",
                ElapsedMs = 20_000
            },
            CancellationToken.None);

        result.Grade.Should().Be(ReviewGrade.Hard);
        result.Feedback!.Correct.Should().BeTrue();
    }
}
