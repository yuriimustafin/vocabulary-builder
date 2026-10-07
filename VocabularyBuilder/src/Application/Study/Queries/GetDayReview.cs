using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Queries;

public record GetDayReviewQuery(Language Language) : IRequest<DayReviewDto>;

public class DayReviewDto
{
    /// <summary>Words to match against their sentences, a group at a time.</summary>
    public List<DayReviewGroupDto> Groups { get; init; } = new();
}

public class DayReviewGroupDto
{
    public List<DayReviewPairDto> Pairs { get; init; } = new();
}

/// <summary>One word and a sentence it fills the gap in.</summary>
public class DayReviewPairDto
{
    public int WordId { get; init; }
    public string Headword { get; init; } = string.Empty;
    public NounArticleDto? Article { get; init; }

    /// <summary>The sentence with the word cut out of it.</summary>
    public string Sentence { get; init; } = string.Empty;

    /// <summary>The form the gap takes, when it is not the headword - "prend" for "prendre".</summary>
    public string? Form { get; init; }

    public string? Translation { get; init; }
}

/// <summary>
/// The day's new words, once more before it ends: each with a sentence it fills the gap in,
/// to be matched up in small groups.
///
/// Practice only. It changes nothing about any word's schedule - the day's graded work is
/// done - so it is free to be as forgiving as matching up a handful of pairs is.
/// </summary>
public class GetDayReviewQueryHandler : IRequestHandler<GetDayReviewQuery, DayReviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IStudyMaterialResolver _materialResolver;
    private readonly StudyOptions _options;
    private readonly TimeProvider _timeProvider;

    public GetDayReviewQueryHandler(
        IApplicationDbContext context,
        IStudyMaterialResolver materialResolver,
        StudyOptions options,
        TimeProvider timeProvider)
    {
        _context = context;
        _materialResolver = materialResolver;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<DayReviewDto> Handle(GetDayReviewQuery request, CancellationToken cancellationToken)
    {
        var dayStart = StudyDay.StartOf(_timeProvider.GetUtcNow().UtcDateTime, _options.DayRolloverHourUtc);

        var cards = await _context.ReviewCards
            .AsNoTracking()
            .Include(c => c.Word).ThenInclude(w => w.Senses)
            .Where(c => c.Word.Language == request.Language)
            .Where(c => c.State != CardState.Suspended && c.State != CardState.New)
            .Where(c => c.IntroducedAtUtc >= dayStart)
            .OrderBy(c => c.IntroducedAtUtc)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var wordIds = cards.Select(c => c.WordId).ToList();

        var content = await _context.WordStudyContents
            .AsNoTracking()
            .Where(c => wordIds.Contains(c.WordId))
            .ToDictionaryAsync(c => c.WordId, cancellationToken);

        var examples = await _context.StudyExamples
            .AsNoTracking()
            .Where(e => wordIds.Contains(e.WordId))
            .ToListAsync(cancellationToken);

        var pairs = new List<DayReviewPairDto>();

        foreach (var card in cards)
        {
            var material = _materialResolver.Resolve(
                card.Word,
                content.GetValueOrDefault(card.WordId),
                new StudyExampleSet(examples.Where(e => e.WordId == card.WordId).ToList(), Array.Empty<string>()));

            // A word with no sentence has nothing to be matched against
            if (!material.HasContextSentence)
            {
                continue;
            }

            pairs.Add(new DayReviewPairDto
            {
                WordId = card.WordId,
                Headword = material.Headword,
                Article = material.Article,
                Sentence = material.BlankedContextSentence,
                Form = string.Equals(material.ContextForm, material.Headword, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : material.ContextForm,
                Translation = material.ContextSentenceTranslation
            });
        }

        var groups = new List<DayReviewGroupDto>();
        var taken = 0;

        foreach (var size in DayReviewGroups.Sizes(pairs.Count))
        {
            groups.Add(new DayReviewGroupDto { Pairs = pairs.Skip(taken).Take(size).ToList() });
            taken += size;
        }

        return new DayReviewDto { Groups = groups };
    }
}

/// <summary>
/// How a day's words are split for matching: as few groups as possible, each of four to six.
/// </summary>
public static class DayReviewGroups
{
    public const int Smallest = 4;
    public const int Largest = 6;

    /// <remarks>
    /// Fewer than two words make no pairs to match, and up to six are one group. Beyond that
    /// the fewest groups of at most six, as evenly sized as they divide. Seven is the one
    /// count no split into fours to sixes fits, and it becomes four and three rather than a
    /// group too large to match comfortably.
    /// </remarks>
    public static IReadOnlyList<int> Sizes(int words)
    {
        if (words < 2)
        {
            return Array.Empty<int>();
        }

        var count = (words + Largest - 1) / Largest;
        var baseSize = words / count;
        var larger = words % count;

        return Enumerable.Range(0, count)
            .Select(i => i < larger ? baseSize + 1 : baseSize)
            .ToList();
    }
}
