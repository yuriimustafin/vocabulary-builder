using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.ImportWords.Queries;

/// <summary>
/// Reduces inflected forms to their lemma using the imported frequency data, which lists
/// every form of a word against the word: "allez" and "vais" both against "aller".
/// </summary>
/// <remarks>
/// This is the deterministic half of lemma resolution and runs before any model is asked.
/// Two reasons it is worth having, in order of importance:
///
/// A lookup cannot drift. Encounter counts only add up while a word resolves to the same
/// string every time; a model that answers "aller" today and "s'en aller" in six months
/// silently splits one word into two, and both counts are then wrong.
///
/// And it is free, which on an exported list of a few hundred terms is most of the
/// conjugations settled without a request.
///
/// One row per form is assumed, which the unique index on (Headword, Language) guarantees
/// and the bundled Lexique data satisfies - its 128,888 forms are distinct. A form that
/// belonged to two lemmas ("suis" to both être and suivre) could not be stored as two rows,
/// and the one row it does get is what this answers from.
///
/// Deliberately narrow. It is only asked about forms that are known to be verbs because a
/// subject pronoun stood in front of them, and it answers only when the data is
/// unambiguous. The same table would reduce plural nouns, and must not be used for them:
/// it maps "ciseaux" to "ciseau" (scissors to chisel), "vacances" to "vacance" (holidays
/// to vacancy) and "feutres" to "feutrer" (markers to the verb "to felt"). Merging those
/// is worse than not merging, because it lands two different words on one entry.
/// </remarks>
public record LookupInflectedFormsQuery : IRequest<IReadOnlyDictionary<string, string>>
{
    /// <summary>Single-word inflected forms, e.g. "allez", "sommes", "finissez".</summary>
    public required IReadOnlyList<string> Forms { get; init; }

    public Language Language { get; init; } = Language.French;
}

public class LookupInflectedFormsQueryHandler
    : IRequestHandler<LookupInflectedFormsQuery, IReadOnlyDictionary<string, string>>
{
    private readonly IApplicationDbContext _context;

    public LookupInflectedFormsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string>> Handle(
        LookupInflectedFormsQuery request,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var forms = request.Forms
            .Where(form => !string.IsNullOrWhiteSpace(form))
            .Select(form => form.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (forms.Count == 0)
        {
            return resolved;
        }

        var rows = await _context.FrequencyWords
            .Where(fw => fw.Language == request.Language && forms.Contains(fw.Headword.ToLower()))
            // The projection walks the navigation itself, so no Include is needed - one
            // query returns each form beside its lemma and nothing else
            .Select(fw => new { fw.Headword, BaseFormHeadword = fw.BaseForm != null ? fw.BaseForm.Headword : null })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            // A form with no lemma above it is a headword in its own right - "est" is also
            // east, "as" an ace, "neige" snow. The frequency data cannot tell which was
            // meant, so the term is left to the model, which can see the pronoun in front
            if (row.BaseFormHeadword == null)
            {
                continue;
            }

            var form = row.Headword.Trim().ToLowerInvariant();
            var lemma = row.BaseFormHeadword.Trim().ToLowerInvariant();

            if (!string.Equals(lemma, form, StringComparison.OrdinalIgnoreCase))
            {
                resolved[form] = lemma;
            }
        }

        return resolved;
    }
}
