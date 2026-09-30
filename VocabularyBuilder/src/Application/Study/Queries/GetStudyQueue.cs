using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
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

    /// <summary>
    /// When the next card falls due, if nothing is servable now. Lets the session say how
    /// long the wait is rather than claiming there is nothing left.
    /// </summary>
    public DateTime? NextDueAtUtc { get; init; }
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

    /// <summary>
    /// The very first showing of this word. There is nothing to grade yet - the learner is
    /// reading it, not recalling it - so the session asks for an acknowledgement instead.
    /// </summary>
    public bool IsIntroduction { get; init; }

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

        // A little grace on minute-scale steps, so the first tests are actually available
        // when the next batch is fetched and end up mixed in among the new words rather
        // than queued behind all of them.
        var due = await DueCardsAsync(
            request.Language, now, now.AddMinutes(_options.InterleaveWindowMinutes),
            limit, cancellationToken);

        // Counting what has already been introduced today is what makes the cap hold
        // however many times the page is refreshed.
        var introducedToday = await _context.ReviewCards
            .CountAsync(c => c.IntroducedAtUtc >= dayStart, cancellationToken);

        var room = new[]
        {
            Math.Max(0, _options.NewCardsPerDay - introducedToday),
            Math.Max(0, limit - due.Count),
            // A few at a time, so the tests for these fall due while the next handful is
            // still being introduced and the two end up mixed together.
            _options.NewCardsPerBatch
        }.Min();

        var newWords = await NewWordSelector.SelectAsync(_context, request.Language, room, cancellationToken);

        // Only when there is genuinely nothing else: pulling a step forward shortens it, so
        // it is a last resort rather than the normal path.
        if (due.Count == 0 && newWords.Count == 0)
        {
            due = await DueCardsAsync(
                request.Language, now, now.AddMinutes(_options.LearnAheadMinutes),
                limit, cancellationToken);
        }

        var wordIds = due.Select(c => c.WordId).Concat(newWords.Select(w => w.Id)).ToList();
        var content = await _context.WordStudyContents
            .AsNoTracking()
            .Where(c => wordIds.Contains(c.WordId))
            .ToDictionaryAsync(c => c.WordId, cancellationToken);

        var examples = await ExamplesFor(wordIds, cancellationToken);

        var pool = await _distractorSource.LoadPoolAsync(
            request.Language, _options.DistractorPoolSize, cancellationToken);

        var rendered = new List<RenderedCard>();
        var waiting = 0;

        foreach (var card in due)
        {
            var outcome = Render(card, card.Word, content, examples, pool, now);

            if (outcome.Card is null)
            {
                if (outcome.Waiting) { waiting++; }
            }
            else
            {
                rendered.Add(outcome.Card);
            }
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

            var outcome = Render(card, word, content, examples, pool, now);

            if (outcome.Card is null)
            {
                if (outcome.Waiting) { waiting++; }
                continue;
            }

            _context.ReviewCards.Add(card);
            word.IsMarkedForStudy = false;
            rendered.Add(outcome.Card with { Introduced = true });
        }

        // Counted from what this call actually created. A card that was introduced earlier
        // today and has not been answered yet is still in the New state, so counting by
        // state would charge it against the day's allowance again on every refresh.
        var introduced = rendered.Count(r => r.Introduced);

        if (introduced > 0)
        {
            // Saving before building the DTOs is what gives the new cards their ids.
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new StudyQueueDto
        {
            Cards = Interleave(rendered).Select(ToDto).ToList(),
            PendingEnrichmentCount = waiting,
            DueCount = due.Count,
            NewToday = introducedToday + introduced,
            NewCardsPerDay = _options.NewCardsPerDay,
            NextDueAtUtc = rendered.Count == 0
                ? await NextDueAtAsync(request.Language, cancellationToken)
                : null
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
        IsIntroduction = rendered.Card.State == CardState.New,
        Exercise = rendered.Exercise
    };

    /// <summary>
    /// Spreads the new words evenly through the cards that are due, so a session reads as
    /// one stream rather than a block of introductions followed by a block of tests.
    /// </summary>
    private static List<RenderedCard> Interleave(List<RenderedCard> rendered)
    {
        var introductions = rendered.Where(r => r.Introduced).ToList();
        var reviews = rendered.Where(r => !r.Introduced).ToList();

        if (introductions.Count == 0 || reviews.Count == 0)
        {
            return rendered;
        }

        var mixed = new List<RenderedCard>(rendered.Count);
        var spacing = (double)reviews.Count / (introductions.Count + 1);
        var placed = 0;

        for (var index = 0; index < reviews.Count; index++)
        {
            mixed.Add(reviews[index]);

            while (placed < introductions.Count && index + 1 >= Math.Round(spacing * (placed + 1)))
            {
                mixed.Add(introductions[placed++]);
            }
        }

        mixed.AddRange(introductions.Skip(placed));
        return mixed;
    }

    /// <summary>
    /// Everything due now, plus any minute-scale step falling due within the grace given.
    ///
    /// Only steps are ever pulled forward. Bringing a day-scale review early would be a real
    /// distortion of the schedule rather than a convenience, so those wait until they are
    /// genuinely due.
    /// </summary>
    private async Task<List<ReviewCard>> DueCardsAsync(
        Language language, DateTime now, DateTime stepCutoff, int limit, CancellationToken cancellationToken)
    {
        return await _context.ReviewCards
            .Include(c => c.Word).ThenInclude(w => w.Senses)
            .Where(c => c.Word.Language == language)
            .Where(c => c.State != CardState.Suspended)
            .Where(c => c.DueAtUtc != null)
            .Where(c => c.DueAtUtc <= now
                || ((c.State == CardState.New
                        || c.State == CardState.Learning
                        || c.State == CardState.Relearning)
                    && c.DueAtUtc <= stepCutoff))
            .OrderBy(c => c.DueAtUtc)
            // Unique tiebreak, for the reason spelled out in NewWordSelector: a Take() over
            // an ordering with ties is re-evaluated for the Include under split queries, and
            // cards introduced in the same batch share a due time to the millisecond.
            .ThenBy(c => c.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private async Task<DateTime?> NextDueAtAsync(Language language, CancellationToken cancellationToken)
    {
        return await _context.ReviewCards
            .Where(c => c.Word.Language == language)
            .Where(c => c.State != CardState.Suspended)
            .Where(c => c.DueAtUtc != null)
            .OrderBy(c => c.DueAtUtc)
            .Select(c => c.DueAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Chooses the rung and renders it, or asks for the word to be filled in and reports it
    /// as still being prepared.
    /// </summary>
    private RenderOutcome Render(
        ReviewCard card,
        Word word,
        IReadOnlyDictionary<int, WordStudyContent> content,
        IReadOnlyDictionary<int, StudyExampleSet> examples,
        IReadOnlyList<DistractorCandidate> pool,
        DateTime now)
    {
        content.TryGetValue(word.Id, out var generated);
        var wordExamples = examples.GetValueOrDefault(word.Id) ?? StudyExampleSet.None;
        var material = _materialResolver.Resolve(word, generated, wordExamples);

        var distractors = _distractorPicker.Pick(material, pool);
        var probe = _ladder.SelectProbe(
            card, word.PartOfSpeech, now,
            type => _catalog.CanBuild(type, material, distractors) && _catalog.Get(type).CanBeProbe);

        var type = probe.Type;

        if (!_catalog.CanBuild(type, material, distractors))
        {
            // Not even the bottom rung can be rendered, which means the word has no usable
            // meaning yet.
            //
            // Reported as waiting only while enrichment might still produce one. A word it
            // has given up on will never render, and counting it would leave the session
            // showing "Preparing 1 word" for ever: there is no automatic retry behind that
            // spinner, so nothing would ever clear it. Skipped quietly instead.
            if (generated?.Status == StudyContentStatus.Failed)
            {
                return RenderOutcome.GivenUp;
            }

            _enrichmentQueue.Enqueue(word.Id);
            return RenderOutcome.Preparing;
        }

        // Studiable, but possibly still missing what only the dictionary holds - a French
        // noun with a generated definition but no gender is shown without its article. Asked
        // for here, after the word is known to be renderable, so a word with nothing to show
        // is not counted as waiting twice. The session carries on meanwhile; the article
        // appears once the fill lands.
        //
        // Only a word enrichment has not finished with is asked for. Enrichment tries the
        // dictionary before anything else, so a Ready or Failed row means it has already been
        // tried; nothing the dictionary did not have is going to appear on the next session
        // either, and a word that cannot be filled would otherwise be asked about on every
        // session for ever. The fill-dictionary sweep is what retries those.
        //
        // Every word is asked, once, for what ties it to things already known - examples
        // around the words it is used with, its origin, a sound-alike - and asked again when
        // it is met in a form no example has yet, which reopens its content. The same
        // Ready-or-Failed rule means a model that could not produce something is not asked
        // again every session: a Ready row from the current prompt has been tried.
        var enrichmentUnfinished = generated is null
            || generated.Status == StudyContentStatus.Pending
            || (generated.Status == StudyContentStatus.Ready && generated.PromptVersion != StudyContentPrompt.Version);

        if (enrichmentUnfinished)
        {
            _enrichmentQueue.Enqueue(word.Id);
        }

        // A probe reached by long-gap escalation is deliberately unhinted: a cue there would
        // inflate a grade that is about to stretch the interval a long way.
        var exercise = _catalog.Get(type).Build(
            material, new ExerciseBuildContext(distractors, AllowHint: !probe.Escalated, CueLevel: probe.CueLevel))
            with { Article = material.Article, Connections = material.Connections };

        return RenderOutcome.Ready(new RenderedCard(card, word.Headword, probe.Rung, exercise));
    }

    /// <summary>Each word's stored examples and the forms it has been met in, by word.</summary>
    private async Task<Dictionary<int, StudyExampleSet>> ExamplesFor(
        List<int> wordIds, CancellationToken cancellationToken)
    {
        var examples = await _context.StudyExamples
            .AsNoTracking()
            .Where(e => wordIds.Contains(e.WordId))
            .ToListAsync(cancellationToken);

        var forms = await _context.WordEncounters
            .AsNoTracking()
            .Where(e => wordIds.Contains(e.WordId) && e.Form != null)
            .Select(e => new { e.WordId, e.Form })
            .Distinct()
            .ToListAsync(cancellationToken);

        return wordIds.Distinct().ToDictionary(
            id => id,
            id => new StudyExampleSet(
                examples.Where(e => e.WordId == id).ToList(),
                forms.Where(f => f.WordId == id).Select(f => f.Form!).ToList()));
    }

    private record RenderedCard(
        ReviewCard Card, string Headword, int Rung, ExercisePayload Exercise, bool Introduced = false);

    /// <summary>
    /// The rendered card, or nothing - and whether the session should say the word is still
    /// being prepared. A word enrichment has given up on is neither rendered nor waited on.
    /// </summary>
    private record RenderOutcome(RenderedCard? Card, bool Waiting)
    {
        public static readonly RenderOutcome GivenUp = new(null, false);
        public static readonly RenderOutcome Preparing = new(null, true);

        public static RenderOutcome Ready(RenderedCard card) => new(card, false);
    }
}
