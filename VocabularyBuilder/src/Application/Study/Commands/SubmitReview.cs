using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Scheduling;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Commands;

public record SubmitReviewCommand : IRequest<ReviewResultDto>
{
    public int CardId { get; init; }

    /// <summary>Identifies this rendering, so a repeat submit scores the card once.</summary>
    public Guid AttemptId { get; init; }

    public ExerciseType ExerciseType { get; init; }

    /// <summary>The chosen option, or the word as assembled.</summary>
    public string? Answer { get; init; }

    /// <summary>The learner's own judgement, for self-graded exercises.</summary>
    public ReviewGrade? SelfGrade { get; init; }

    public int ElapsedMs { get; init; }
    public int Resets { get; init; }
    public bool HintUsed { get; init; }
    public bool Abandoned { get; init; }
}

public class ReviewResultDto
{
    public ReviewGrade Grade { get; init; }
    public CardState State { get; init; }
    public int IntervalDays { get; init; }
    public int Rung { get; init; }
    public CardDifficulty Difficulty { get; init; }
    public DateTime? NextDueAtUtc { get; init; }

    /// <summary>Ungraded re-encoding exercises to play out before moving on.</summary>
    public List<FollowUpDto> FollowUps { get; init; } = new();

    /// <summary>True when this submit matched one already recorded and changed nothing.</summary>
    public bool WasDuplicate { get; init; }

    /// <summary>Set for automatically graded exercises; null when the learner graded themselves.</summary>
    public ReviewFeedbackDto? Feedback { get; init; }
}

public class FollowUpDto
{
    public ExercisePayload Exercise { get; init; } = null!;
}

/// <summary>
/// What to show once an automatically graded answer has been marked.
///
/// The word is shown in full whether or not it was right, because seeing it again is worth
/// as much after a correct answer as after a wrong one. Self-graded exercises need none of
/// this: the learner has already revealed the answer themselves.
/// </summary>
public class ReviewFeedbackDto
{
    public bool Correct { get; init; }

    public string Headword { get; init; } = string.Empty;
    public NounArticleDto? Article { get; init; }
    public string? Meaning { get; init; }
    public string? Transcription { get; init; }
    public string? PartOfSpeech { get; init; }
    public string? ContextSentence { get; init; }

    /// <summary>What was picked instead, when the answer was wrong.</summary>
    public ChosenAnswerDto? Chosen { get; init; }
}

/// <summary>
/// The option that was chosen by mistake, named.
///
/// A wrong option is another real word from the collection, so it can be identified rather
/// than just marked wrong - the meaning that was picked belongs to some word, and the word
/// that was picked has a meaning of its own.
/// </summary>
public class ChosenAnswerDto
{
    public string Text { get; init; } = string.Empty;
    public string? Headword { get; init; }
    public string? Meaning { get; init; }
}

/// <summary>
/// Records one graded answer: grade it, reschedule, move the rung, log it, and work out
/// what re-encoding should follow.
///
/// The probe is the only thing graded. Follow-ups are support, and are logged as such so
/// they can never feed back into ease, interval or the success average.
/// </summary>
public class SubmitReviewCommandHandler : IRequestHandler<SubmitReviewCommand, ReviewResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IReviewScheduler _scheduler;
    private readonly IExerciseLadder _ladder;
    private readonly IExerciseCatalog _catalog;
    private readonly IStudyMaterialResolver _materialResolver;
    private readonly IDistractorPicker _distractorPicker;
    private readonly IDistractorSource _distractorSource;
    private readonly ICardDifficultyCalculator _difficulty;
    private readonly IScaffoldSequencer _scaffolds;
    private readonly IStudyWordLookup _wordLookup;
    private readonly StudyOptions _options;
    private readonly TimeProvider _timeProvider;

    public SubmitReviewCommandHandler(
        IApplicationDbContext context,
        IReviewScheduler scheduler,
        IExerciseLadder ladder,
        IExerciseCatalog catalog,
        IStudyMaterialResolver materialResolver,
        IDistractorPicker distractorPicker,
        IDistractorSource distractorSource,
        ICardDifficultyCalculator difficulty,
        IScaffoldSequencer scaffolds,
        IStudyWordLookup wordLookup,
        StudyOptions options,
        TimeProvider timeProvider)
    {
        _context = context;
        _scheduler = scheduler;
        _ladder = ladder;
        _catalog = catalog;
        _materialResolver = materialResolver;
        _distractorPicker = distractorPicker;
        _distractorSource = distractorSource;
        _difficulty = difficulty;
        _scaffolds = scaffolds;
        _wordLookup = wordLookup;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<ReviewResultDto> Handle(SubmitReviewCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var card = await _context.ReviewCards
            .Include(c => c.Word).ThenInclude(w => w.Senses)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken);

        Guard.Against.NotFound(request.CardId, card);

        // A double click or a retry after a timeout must not score the card twice.
        var alreadyRecorded = await _context.ReviewLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.AttemptId == request.AttemptId, cancellationToken);

        if (alreadyRecorded is not null)
        {
            return Duplicate(card, alreadyRecorded);
        }

        var generated = await _context.WordStudyContents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.WordId == card.WordId, cancellationToken);

        var material = _materialResolver.Resolve(card.Word, generated);
        var definition = _catalog.Get(request.ExerciseType);

        var grade = definition.Resolve(
            new ExerciseAnswer(
                request.Answer,
                request.SelfGrade,
                request.ElapsedMs,
                request.Resets,
                request.HintUsed,
                request.Abandoned),
            material);

        var before = Snapshot(card);
        var scheduling = _scheduler.Schedule(card, grade, now);

        Apply(card, grade, scheduling, now);

        _context.ReviewLogs.Add(new ReviewLog
        {
            ReviewCardId = card.Id,
            AttemptId = request.AttemptId,
            ReviewedAtUtc = now,
            ExerciseType = request.ExerciseType,
            Grade = grade,
            GradeWasSelfReported = definition.GradingMode == GradingMode.SelfReported,
            IsScaffold = false,
            ElapsedMs = request.ElapsedMs,
            HintUsed = request.HintUsed,
            StateBefore = before.State,
            RungBefore = before.Rung,
            IntervalBeforeDays = before.IntervalDays,
            IntervalAfterDays = card.IntervalDays,
            EaseFactorAfter = card.EaseFactor
        });

        await _context.SaveChangesAsync(cancellationToken);

        var difficulty = _difficulty.Calculate(card);

        return new ReviewResultDto
        {
            Grade = grade,
            State = card.State,
            IntervalDays = card.IntervalDays,
            Rung = card.CurrentRung,
            Difficulty = difficulty,
            NextDueAtUtc = card.DueAtUtc,
            FollowUps = await BuildFollowUps(
                card, material, before.Rung, grade, difficulty, cancellationToken),
            Feedback = definition.GradingMode == GradingMode.Automatic
                ? await BuildFeedback(card, material, request, grade, cancellationToken)
                : null
        };
    }

    private void Apply(ReviewCard card, ReviewGrade grade, SchedulingResult scheduling, DateTime now)
    {
        card.RecentSuccessRate = _difficulty.NextSuccessRate(card.RecentSuccessRate, grade);
        card.CurrentRung = _ladder.NextRung(card.CurrentRung, grade, card.RecentSuccessRate);

        if (scheduling.IsLapse)
        {
            card.Lapses++;
            card.LapsesSinceRecovery++;
        }

        card.State = scheduling.State;
        card.IntervalDays = scheduling.IntervalDays;
        card.EaseFactor = scheduling.EaseFactor;
        card.LearningStepIndex = scheduling.LearningStepIndex;
        card.DueAtUtc = scheduling.DueAtUtc;
        card.LastReviewedAtUtc = now;
        card.ReviewNumber++;

        // Recovering clears the recent-failure count, so one bad week does not mark a word
        // as difficult for good.
        if (_difficulty.Calculate(card) == CardDifficulty.Comfortable)
        {
            card.LapsesSinceRecovery = 0;
        }
    }

    /// <summary>
    /// Describes what was marked, so the learner sees the word again either way and can tell
    /// what they picked instead when they missed it.
    /// </summary>
    private async Task<ReviewFeedbackDto> BuildFeedback(
        ReviewCard card,
        StudyMaterial material,
        SubmitReviewCommand request,
        ReviewGrade grade,
        CancellationToken cancellationToken)
    {
        var correct = grade > ReviewGrade.Again;

        return new ReviewFeedbackDto
        {
            Correct = correct,
            Headword = material.Headword,
            Article = material.Article,
            Meaning = material.Meaning,
            Transcription = material.Transcription,
            PartOfSpeech = material.PartOfSpeech,
            ContextSentence = material.ContextSentence,
            Chosen = correct ? null : await DescribeChoice(card, request, cancellationToken)
        };
    }

    /// <summary>
    /// Names the option that was picked by mistake. Which way round depends on the
    /// question: choosing a meaning identifies a word, choosing a word identifies a meaning.
    /// </summary>
    private async Task<ChosenAnswerDto?> DescribeChoice(
        ReviewCard card, SubmitReviewCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Answer))
        {
            return null;
        }

        var language = card.Word.Language;

        var chosen = request.ExerciseType switch
        {
            ExerciseType.WordToMeaningChoice =>
                await _wordLookup.ByMeaningAsync(language, request.Answer, cancellationToken),
            ExerciseType.MeaningToWordChoice =>
                await _wordLookup.ByHeadwordAsync(language, request.Answer, cancellationToken),
            _ => null
        };

        return new ChosenAnswerDto
        {
            Text = request.Answer,
            Headword = chosen?.Headword,
            Meaning = chosen?.Meaning
        };
    }

    /// <summary>
    /// Renders the re-encoding exercises that follow the graded probe. These are never
    /// scored, so they are free to hand out as much support as the word needs.
    /// </summary>
    private async Task<List<FollowUpDto>> BuildFollowUps(
        ReviewCard card,
        StudyMaterial material,
        int probeRung,
        ReviewGrade grade,
        CardDifficulty difficulty,
        CancellationToken cancellationToken)
    {
        var steps = _scaffolds.Build(probeRung, grade, difficulty, material.Headword.Length);

        if (steps.Count == 0)
        {
            return new List<FollowUpDto>();
        }

        var pool = await _distractorSource.LoadPoolAsync(
            card.Word.Language, _options.DistractorPoolSize, cancellationToken);
        var distractors = _distractorPicker.Pick(material, pool);

        var followUps = new List<FollowUpDto>();

        foreach (var step in steps)
        {
            if (!_catalog.CanBuild(step.Type, material, distractors))
            {
                continue;
            }

            followUps.Add(new FollowUpDto
            {
                Exercise = _catalog.Get(step.Type).Build(
                    material, new ExerciseBuildContext(distractors, step.RevealedLetters))
                    with { Article = material.Article }
            });
        }

        return followUps;
    }

    private ReviewResultDto Duplicate(ReviewCard card, ReviewLog recorded) => new()
    {
        Grade = recorded.Grade,
        State = card.State,
        IntervalDays = card.IntervalDays,
        Rung = card.CurrentRung,
        Difficulty = _difficulty.Calculate(card),
        NextDueAtUtc = card.DueAtUtc,
        WasDuplicate = true
    };

    private static CardSnapshot Snapshot(ReviewCard card) =>
        new(card.State, card.CurrentRung, card.IntervalDays);

    private record CardSnapshot(CardState State, int Rung, int IntervalDays);
}
