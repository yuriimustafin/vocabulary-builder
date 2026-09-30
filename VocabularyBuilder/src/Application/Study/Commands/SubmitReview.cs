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

    /// <summary>The example sentence the exercise was built on, as the payload gave it.</summary>
    public int? ExampleId { get; init; }

    /// <summary>Every option ticked, for an exercise with more than one right answer.</summary>
    public List<string>? Selections { get; init; }
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

    /// <summary>
    /// A miss on a mistake-tolerant exercise, which cost the word nothing: it keeps its level
    /// and its schedule and comes back shortly to be asked another way.
    /// </summary>
    public bool Tolerated { get; init; }

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

    /// <summary>Which sense the meaning is, in the language being learned.</summary>
    public string? MeaningGloss { get; init; }

    /// <summary>What the context sentence says, in the learner's language.</summary>
    public string? ContextSentenceTranslation { get; init; }

    /// <summary>What was picked instead, when the answer was wrong.</summary>
    public ChosenAnswerDto? Chosen { get; init; }

    /// <summary>
    /// For a typed answer that was accepted but not exactly right, what was off - so the
    /// learner sees why it did not count in full.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>What ties the word to things already known, shown beside the word.</summary>
    public WordConnectionsDto? Connections { get; init; }

    /// <summary>For an exercise with several right answers, what they were.</summary>
    public List<string>? ExpectedOptions { get; init; }
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
    private readonly ILearningExitCriterion _learningExit;
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
        ILearningExitCriterion learningExit,
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
        _learningExit = learningExit;
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

        var examples = await _context.StudyExamples
            .Where(e => e.WordId == card.WordId)
            .ToListAsync(cancellationToken);

        // The forms met in reading weigh the choice of sentence exactly as they did when the
        // queue built the exercise, so one that carried no example id is still resolved on
        // the sentence it showed
        var forms = await _context.WordEncounters
            .AsNoTracking()
            .Where(e => e.WordId == card.WordId && e.Form != null)
            .Select(e => e.Form!)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Resolved on the sentence that was asked, so the feedback shows that one
        var material = _materialResolver.Resolve(
            card.Word, generated, new StudyExampleSet(examples, forms, request.ExampleId));
        var definition = _catalog.Get(request.ExerciseType);

        var answer = new ExerciseAnswer(
            request.Answer,
            request.SelfGrade,
            request.ElapsedMs,
            request.Resets,
            request.HintUsed,
            request.Abandoned,
            request.Selections);

        // A typed answer is matched once, and graded on that match - a "slip" that spells
        // another word included, which the match already says is wrong
        var typedDefinition = definition as ITypedExerciseDefinition;
        var typed = typedDefinition is null
            ? null
            : await CheckTypedAnswer(typedDefinition, answer, card, material, cancellationToken);
        var grade = typed is null
            ? definition.Resolve(answer, material)
            : typedDefinition!.Resolve(answer, material, typed);

        var before = Snapshot(card);

        // A miss on a mistake-tolerant exercise costs the word nothing: it stays where it
        // was, on its level and its schedule, and comes back shortly to be asked another way
        var tolerated = grade == ReviewGrade.Again && _ladder.IsTolerant(request.ExerciseType);

        var move = tolerated
            ? new RungMove(card.CurrentRung, card.RungStreak)
            : _ladder.NextRung(card, grade, request.HintUsed);
        var learning = before.State is CardState.New or CardState.Learning or CardState.Relearning;
        // A tolerated miss is not a try either, or enough of them would force the word out
        // of learning without it ever meeting the criterion
        var retrievals = learning ? card.PhaseRetrievals + (tolerated ? 0 : 1) : 0;
        var learningComplete = learning && _learningExit.IsMet(
            before.State, grade, move, retrievals, card.LastReviewedAtUtc, now);
        var scheduling = tolerated
            ? _scheduler.Hold(card, now)
            : _scheduler.Schedule(card, grade, now, learningComplete);

        Apply(card, grade, move, retrievals, scheduling, request.ExerciseType, tolerated, now);
        RecordExampleUse(examples, material, request, grade, now);

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
            Tolerated = tolerated,
            FollowUps = await BuildFollowUps(
                card, material, before.Rung, grade, difficulty, tolerated, cancellationToken),
            Feedback = definition.GradingMode == GradingMode.Automatic
                ? await BuildFeedback(card, definition, material, request, grade, typed, tolerated, cancellationToken)
                : null
        };
    }

    /// <summary>
    /// For a typed exercise, how the answer compared with the word. A one-letter slip is
    /// forgiven only while it does not spell another word in the collection - poisson typed
    /// for poison is a different word known, not this one nearly known.
    /// </summary>
    private async Task<TypedMatch> CheckTypedAnswer(
        ITypedExerciseDefinition typedDefinition,
        ExerciseAnswer answer,
        ReviewCard card,
        StudyMaterial material,
        CancellationToken cancellationToken)
    {
        var match = typedDefinition.Match(answer, material);

        if (match.Kind is TypedMatchKind.Typo or TypedMatchKind.AccentsOnly)
        {
            var other = await _wordLookup.ByHeadwordAsync(card.Word.Language, match.Word, cancellationToken);

            if (other is not null && other.WordId != card.WordId)
            {
                return match with { Kind = TypedMatchKind.Wrong };
            }
        }

        return match;
    }

    /// <summary>
    /// Records the answer against the example sentence it was asked on. A sentence answered
    /// correctly goes to the back of the queue, so the word is next asked on one it has not
    /// yet practised; one that was missed stays at the front.
    /// </summary>
    private static void RecordExampleUse(
        List<StudyExample> examples, StudyMaterial material, SubmitReviewCommand request, ReviewGrade grade, DateTime now)
    {
        // Only an id the payload carried, and only for the sentence the material resolved
        // to - an id for another word's example is ignored rather than trusted
        if (request.ExampleId is not { } id || material.ExampleId != id)
        {
            return;
        }

        var example = examples.First(e => e.Id == id);
        example.LastUsedAtUtc = now;

        if (grade >= ReviewGrade.Good)
        {
            example.Successes++;
        }
    }

    private void Apply(
        ReviewCard card,
        ReviewGrade grade,
        RungMove move,
        int retrievals,
        SchedulingResult scheduling,
        ExerciseType exerciseType,
        bool tolerated,
        DateTime now)
    {
        // A tolerated miss is not evidence against the word, so it does not reach its record
        if (!tolerated)
        {
            card.RecentSuccessRate = _difficulty.NextSuccessRate(card.RecentSuccessRate, grade);
        }

        card.CurrentRung = move.Rung;
        card.RungStreak = move.Streak;
        card.LastExerciseType = exerciseType;

        // Counted through one learning phase, and cleared when it ends - by graduating, or
        // by a lapse starting a new one.
        card.PhaseRetrievals = scheduling.State == CardState.Review || scheduling.IsLapse ? 0 : retrievals;

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
        IExerciseDefinition definition,
        StudyMaterial material,
        SubmitReviewCommand request,
        ReviewGrade grade,
        TypedMatch? typed,
        bool tolerated,
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
            MeaningGloss = material.MeaningGloss,
            ContextSentenceTranslation = material.ContextSentenceTranslation,
            Chosen = correct ? null : await DescribeChoice(card, request, typed, cancellationToken),
            Note = tolerated
                ? "This one does not count against the word - it will be asked again another way."
                : typed is null ? null : NoteFor(typed.Kind, material),
            Connections = material.Connections,
            ExpectedOptions = definition.ExpectedOptions(material)?.ToList()
        };
    }

    private static string? NoteFor(TypedMatchKind kind, StudyMaterial material) => kind switch
    {
        TypedMatchKind.AccentsOnly => "Nearly - mind the accents.",
        TypedMatchKind.Typo => "Nearly - one letter out.",
        TypedMatchKind.WrongArticle => $"Right word, other gender: it is {material.Article?.Definite} {material.Headword}.",
        _ => null
    };

    /// <summary>
    /// Names the option that was picked by mistake. Which way round depends on the
    /// question: choosing a meaning identifies a word, choosing a word identifies a meaning.
    /// A typed word that is another one in the collection - poisson for poison - is named
    /// the same way, so the learner sees which word they did know.
    /// </summary>
    private async Task<ChosenAnswerDto?> DescribeChoice(
        ReviewCard card, SubmitReviewCommand request, TypedMatch? typed, CancellationToken cancellationToken)
    {
        if (request.Selections is { Count: > 0 } ticked)
        {
            return new ChosenAnswerDto { Text = string.Join(", ", ticked) };
        }

        if (string.IsNullOrWhiteSpace(request.Answer))
        {
            return null;
        }

        var language = card.Word.Language;

        var chosen = typed is not null
            ? await OtherWordTyped(card, typed, cancellationToken)
            : request.ExerciseType switch
            {
                ExerciseType.WordToMeaningChoice =>
                    await _wordLookup.ByMeaningAsync(language, request.Answer, cancellationToken),
                ExerciseType.MeaningToWordChoice or ExerciseType.ContextToWordChoice =>
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

    /// <summary>The word the typed answer spells, when it is a different one from the word asked.</summary>
    private async Task<StudyWordSummary?> OtherWordTyped(
        ReviewCard card, TypedMatch typed, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typed.Word))
        {
            return null;
        }

        var other = await _wordLookup.ByHeadwordAsync(card.Word.Language, typed.Word, cancellationToken);
        return other is not null && other.WordId != card.WordId ? other : null;
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
        bool tolerated,
        CancellationToken cancellationToken)
    {
        var steps = _scaffolds.Build(probeRung, grade, difficulty, material.Headword.Length, tolerated);

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
            // A follow-up is not marked, so an exercise that exists to mark a typed answer
            // has nothing to offer there; the first other one that can be built is used.
            var type = step.Candidates
                .Cast<ExerciseType?>()
                .FirstOrDefault(candidate =>
                    _catalog.CanBuild(candidate!.Value, material, distractors)
                    && _catalog.Get(candidate.Value) is not ITypedExerciseDefinition);

            if (type is null)
            {
                continue;
            }

            followUps.Add(new FollowUpDto
            {
                Exercise = _catalog.Get(type.Value).Build(
                    material, new ExerciseBuildContext(distractors, step.RevealedLetters))
                    with { Article = material.Article, Connections = material.Connections }
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
