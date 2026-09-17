using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Queries;

/// <summary>
/// Picks the words to introduce next, in equal shares from three different ideas of what
/// is worth learning:
///
///   - words met most often, which are the ones actually getting in the way
///   - the most common words not yet known, which pay back the most per word learned
///   - words explicitly marked for study, which are whatever the learner decided matters
///
/// Splitting evenly stops any one of those crowding the others out: a long tail of Kindle
/// highlights would otherwise bury the frequency list, and a hand-picked word would wait
/// behind both.
/// </summary>
public static class NewWordSelector
{
    public static async Task<List<Word>> SelectAsync(
        IApplicationDbContext context, Language language, int count, CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return new List<Word>();
        }

        // A word with a card has been introduced already; one without has not.
        //
        // Words marked Known are skipped. Saying so is the only way to decline a word for
        // good: a card is what records that a word has been seen, so dismissing one without
        // this would just mean it came back tomorrow.
        var candidates = context.Words
            .Include(w => w.Senses)
            .Include(w => w.WordEncounters)
            .Where(w => w.Language == language)
            .Where(w => w.Status != WordStatus.Known)
            .Where(w => !context.ReviewCards.Any(c => c.WordId == w.Id));

        var share = Math.Max(1, count / 3);

        // Ordered by id rather than by LastModified: SQLite cannot sort a DateTimeOffset in
        // SQL, and LastModified would not mean "when it was marked" anyway, since any edit
        // moves it. Oldest word first is both translatable and stable.
        var marked = await candidates
            .Where(w => w.IsMarkedForStudy)
            .OrderBy(w => w.Id)
            .Take(share)
            .ToListAsync(cancellationToken);

        var mostEncountered = await candidates
            .Where(w => w.WordEncounters.Count > 0)
            .OrderByDescending(w => w.WordEncounters.Count)
            .ThenBy(w => w.Frequency == null)
            .ThenBy(w => w.Frequency)
            .Take(share)
            .ToListAsync(cancellationToken);

        var mostCommon = await candidates
            .Where(w => w.Frequency != null)
            .OrderBy(w => w.Frequency)
            .Take(share)
            .ToListAsync(cancellationToken);

        var selected = Merge(count, marked, mostEncountered, mostCommon);

        // A short bucket gives its share back rather than shrinking the day's session.
        if (selected.Count < count)
        {
            var topUp = await candidates
                .OrderBy(w => w.Frequency == null)
                .ThenBy(w => w.Frequency)
                .ThenBy(w => w.Id)
                .Take(count + selected.Count)
                .ToListAsync(cancellationToken);

            selected = Merge(count, selected, topUp);
        }

        return selected;
    }

    /// <summary>
    /// Takes from each bucket in turn so the shares interleave, skipping anything already
    /// chosen - the buckets overlap, and a word should not take two of the day's places.
    /// </summary>
    private static List<Word> Merge(int count, params List<Word>[] buckets)
    {
        var selected = new List<Word>();
        var seen = new HashSet<int>();

        for (var index = 0; selected.Count < count; index++)
        {
            var exhausted = true;

            foreach (var bucket in buckets)
            {
                if (index >= bucket.Count)
                {
                    continue;
                }

                exhausted = false;
                var word = bucket[index];

                if (seen.Add(word.Id))
                {
                    selected.Add(word);
                }

                if (selected.Count == count)
                {
                    return selected;
                }
            }

            if (exhausted)
            {
                break;
            }
        }

        return selected;
    }
}
