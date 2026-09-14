using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Study.Enrichment;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Queries;

public record GetStudyQueueQuery(Language Language, int? Limit = null) : IRequest<StudyQueueDto>;

public class StudyQueueDto
{
    public List<StudyCardDto> Cards { get; init; } = new();

    /// <summary>Words picked for today that are still being filled in. The page waits on these.</summary>
    public int PendingEnrichmentCount { get; init; }

    public int DueCount { get; init; }

    /// <summary>New words introduced today, against the daily cap.</summary>
    public int NewToday { get; init; }

    public int NewCardsPerDay { get; init; }
}

public class StudyCardDto
{
    public int CardId { get; init; }
    public int WordId { get; init; }
    public string Headword { get; init; } = string.Empty;

    /// <summary>Generated per rendering; returned with the answer so a repeat submit scores once.</summary>
    public Guid AttemptId { get; init; }

    public int Rung { get; init; }
    public CardState State { get; init; }
    public CardDifficulty Difficulty { get; init; }
    public bool IsNew { get; init; }
    public ExercisePayload Exercise { get; init; } = null!;
}

/// <summary>
/// Builds a session: everything due, plus whatever is left of today's allowance of new
/// words, with an exercise already chosen and rendered for each.
///
/// Words that cannot be rendered yet are not held back - they are queued for filling in and
/// reported as a waiting count, so the session starts on what is ready instead of blocking
/// on a model call.
/// </summary>
public class GetStudyQueueQueryHandler : IRequestHandler<GetStudyQueueQuery, StudyQueueDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IExerciseLadder _ladder;
    private readonly IExerciseCatalog _catalog;
    private readonly IStudyMaterialResolver _materialResolver;
    private readonly IDistractorPicker _distractorPicker;
    private readonly IDistractorSource _distractorSource;
    private readonly ICardDifficultyCalculator _difficulty;
    private readonly IStudyEnrichmentQueue _enrichmentQueue;
    private readonly StudyOptions _options;
    private readonly TimeProvider _timeProvider;

    public GetStudyQueueQueryHandler(
        IApplicationDbContext context,
        IExerciseLadder ladder,
        IExerciseCatalog catalog,
        IStudyMaterialResolver materialResolver,
        IDistractorPicker distractorPicker,
        IDistractorSource distractorSource,
        ICardDifficultyCalculator difficulty,
        IStudyEnrichmentQueue enrichmentQueue,
        StudyOptions options,
        TimeProvider timeProvider)
    {
        _context = context;
        _ladder = ladder;
        _catalog = catalog;
        _materialResolver = materialResolver;
        _distractorPicker = distractorPicker;
        _distractorSource = distractorSource;
        _difficulty = difficulty;
        _enrichmentQueue = enrichmentQueue;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<StudyQueueDto> Handle(GetStudyQueueQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var dayStart = StudyDay.StartOf(now, _options.DayRolloverHourUtc);
        var limit = Math.Clamp(request.Limit ?? _options.MaxCardsPerSession, 1, _options.MaxCardsPerSession);

        var due = await DueCardsAsync(request.Language, now, limit, cancellationToken);

        // Counting what has already been introduced today is what makes the cap hold
        // however many times the page is refreshed.
        var introducedToday = await _context.ReviewCards
            .CountAsync(c => c.IntroducedAtUtc >= dayStart, cancellationToken);

        var room = Math.Min(Math.Max(0, _options.NewCardsPerDay - introducedToday), limit - due.Count);
        var newWords = await NewWordSelector.SelectAsync(_context, request.Language, room, cancellationToken);

        var wordIds = due.Select(c => c.WordId).Concat(newWords.Select(w => w.Id)).ToList();
        var content = await _context.WordStudyContents
            .AsNoTracking()
            .Where(c => wordIds.Contains(c.WordId))
            .ToDictionaryAsync(c => c.WordId, cancellationToken);

        var pool = await _distractorSource.LoadPoolAsync(
            request.Language, _options.DistractorPoolSize, cancellationToken);

        var rendered = new List<RenderedCard>();
        var waiting = 0;

        foreach (var card in due)
        {
            var result = Render(card, card.Word, content, pool, now);
            if (result is null) { waiting++; } else { rendered.Add(result); }
        }

        foreach (var word in newWords)
        {
            // A word has no card until it is actually introduced, so a word that cannot be
            // rendered yet leaves no trace and simply waits for its content.
            var card = new ReviewCard
            {
                WordId = word.Id,
                State = CardState.New,
                CurrentRung = 0,
                DueAtUtc = now,
                IntroducedAtUtc = now
            };

            var result = Render(card, word, content, pool, now);

            if (result is null)
            {
                waiting++;
                continue;
            }

            _context.ReviewCards.Add(card);
            word.IsMarkedForStudy = false;
            rendered.Add(result);
        }

        var introduced = rendered.Count(r => r.Card.State == CardState.New);

        if (introduced > 0)
        {
            // Saving before building the DTOs is what gives the new cards their ids.
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new StudyQueueDto
        {
            Cards = rendered.Select(ToDto).ToList(),
            PendingEnrichmentCount = waiting,
            DueCount = due.Count,
            NewToday = introducedToday + introduced,
            NewCardsPerDay = _options.NewCardsPerDay
        };
    }

    private StudyCardDto ToDto(RenderedCard rendered) => new()
    {
        CardId = rendered.Card.Id,
        WordId = rendered.Card.WordId,
        Headword = rendered.Headword,
        AttemptId = Guid.NewGuid(),
        Rung = rendered.Rung,
        State = rendered.Card.State,
        Difficulty = _difficulty.Calculate(rendered.Card),
        IsNew = rendered.Card.State == CardState.New,
        Exercise = rendered.Exercise
    };

    private async Task<List<ReviewCard>> DueCardsAsync(
        Language language, DateTime now, int limit, CancellationToken cancellationToken)
    {
        return await _context.ReviewCards
            .Include(c => c.Word).ThenInclude(w => w.Senses)
            .Where(c => c.Word.Language == language)
            .Where(c => c.State != CardState.Suspended)
            .Where(c => c.DueAtUtc != null && c.DueAtUtc <= now)
            .OrderBy(c => c.DueAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Chooses the rung and renders it, or returns null after asking for the word to be
    /// filled in.
    /// </summary>
    private RenderedCard? Render(
        ReviewCard card,
        Word word,
        IReadOnlyDictionary<int, WordStudyContent> content,
        IReadOnlyList<DistractorCandidate> pool,
        DateTime now)
    {
        content.TryGetValue(word.Id, out var generated);
        var material = _materialResolver.Resolve(word, generated);

        var distractors = _distractorPicker.Pick(material, pool);
        var rung = _ladder.SelectProbeRung(
            card, word.PartOfSpeech, now, type => _catalog.CanBuild(type, material, distractors));

        var type = _ladder.TypeAt(rung);

        if (!_catalog.CanBuild(type, material, distractors))
        {
            // Not even the bottom rung can be rendered, which means the word has no usable
            // meaning yet.
            _enrichmentQueue.Enqueue(word.Id);
            return null;
        }

        // A probe raised above the card's own rung was reached by long-gap escalation, and
        // is deliberately unhinted: a cue there would inflate a grade that is about to
        // stretch the interval a long way.
        var escalated = rung > card.CurrentRung;

        var exercise = _catalog.Get(type).Build(
            material, new ExerciseBuildContext(distractors, AllowHint: !escalated));

        return new RenderedCard(card, word.Headword, rung, exercise);
    }

    private record RenderedCard(ReviewCard Card, string Headword, int Rung, ExercisePayload Exercise);
}
