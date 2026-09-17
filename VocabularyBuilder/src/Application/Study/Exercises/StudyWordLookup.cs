using System.Linq.Expressions;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// A word, as far as answer feedback needs to describe it.
/// </summary>
public record StudyWordSummary(int WordId, string Headword, string? Meaning, string? PartOfSpeech);

public interface IStudyWordLookup
{
    Task<StudyWordSummary?> ByHeadwordAsync(Language language, string headword, CancellationToken cancellationToken);

    Task<StudyWordSummary?> ByMeaningAsync(Language language, string meaning, CancellationToken cancellationToken);
}

/// <summary>
/// Finds the word a chosen option belongs to.
///
/// Wrong answers are other words from the collection, so a miss is worth explaining: the
/// meaning that was picked belongs to something, and saying what turns a bare "wrong" into
/// a second word seen in passing. The option only comes back as text, which is what keeps
/// the answer out of the payload in the first place, so it has to be matched here.
/// </summary>
public class StudyWordLookup : IStudyWordLookup
{
    private readonly IApplicationDbContext _context;

    public StudyWordLookup(IApplicationDbContext context) => _context = context;

    public Task<StudyWordSummary?> ByHeadwordAsync(
        Language language, string headword, CancellationToken cancellationToken)
    {
        var trimmed = headword.Trim();

        return FindAsync(language, w => w.Headword == trimmed, cancellationToken);
    }

    public Task<StudyWordSummary?> ByMeaningAsync(
        Language language, string meaning, CancellationToken cancellationToken)
    {
        var trimmed = meaning.Trim();

        // Either source of a definition will do - the same precedence the distractor pool
        // draws on, so anything that could have been offered can also be explained.
        return FindAsync(
            language,
            w => w.Senses!.Any(s => s.Definition == trimmed)
                || _context.WordStudyContents.Any(c => c.WordId == w.Id && c.GeneratedDefinition == trimmed),
            cancellationToken);
    }

    /// <summary>
    /// The match runs against the stored columns and only then projects. Filtering the
    /// projection instead would put a subquery inside the predicate, which does not
    /// translate.
    /// </summary>
    private async Task<StudyWordSummary?> FindAsync(
        Language language, Expression<Func<Word, bool>> predicate, CancellationToken cancellationToken)
    {
        return await _context.Words
            .AsNoTracking()
            .Where(w => w.Language == language)
            .Where(predicate)
            .Select(w => new StudyWordSummary(
                w.Id,
                w.Headword,
                w.Senses!.Select(s => s.Definition).FirstOrDefault()
                    ?? _context.WordStudyContents
                        .Where(c => c.WordId == w.Id)
                        .Select(c => c.GeneratedDefinition)
                        .FirstOrDefault(),
                w.PartOfSpeech))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
