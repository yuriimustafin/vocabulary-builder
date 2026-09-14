using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

public interface IDistractorSource
{
    /// <summary>
    /// Loads the pool of words a session can draw wrong answers from. One query per
    /// session rather than one per card, since every card in a session shares a language.
    /// </summary>
    Task<IReadOnlyList<DistractorCandidate>> LoadPoolAsync(
        Language language, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// Reads distractor candidates out of the learner's own collection.
///
/// Only words that already carry a definition are useful as wrong meanings, so the query
/// filters on that up front rather than loading the whole vocabulary and discarding most
/// of it. Common words are preferred because they make plausible, recognisable decoys.
/// </summary>
public class DistractorSource : IDistractorSource
{
    private readonly IApplicationDbContext _context;

    public DistractorSource(IApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<DistractorCandidate>> LoadPoolAsync(
        Language language, int limit, CancellationToken cancellationToken)
    {
        return await _context.Words
            .AsNoTracking()
            .Where(w => w.Language == language)
            .OrderBy(w => w.Frequency == null)
            .ThenBy(w => w.Frequency)
            .Take(limit)
            .Select(w => new DistractorCandidate(
                w.Id,
                w.Headword,
                w.PartOfSpeech,
                w.Frequency,
                w.Senses!.Select(s => s.Definition).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
