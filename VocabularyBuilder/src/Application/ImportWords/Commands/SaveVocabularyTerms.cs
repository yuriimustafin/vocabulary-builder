using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.ImportWords.Commands;

/// <param name="SourceTerm">The term as the source wrote it, which makes the encounter distinct.</param>
/// <param name="Lemma">The headword the word is stored under.</param>
/// <param name="Notes">Anything worth keeping about this particular encounter, such as the sentence it was met in.</param>
public record ImportedTerm(string SourceTerm, string Lemma, string? Notes = null);

public class SaveVocabularyTermsResult
{
    /// <summary>Words that did not exist before this import.</summary>
    public int WordsCreated { get; set; }

    /// <summary>Encounters this import added, across new and existing words.</summary>
    public int EncountersCreated { get; set; }

    /// <summary>Distinct headwords this import touched, new or already known.</summary>
    public List<string> Lemmas { get; set; } = new();
}

/// <summary>
/// Writes resolved terms to the vocabulary, one encounter per source term.
/// </summary>
/// <remarks>
/// The shared tail of both French imports. Two things matter here:
///
/// Terms are stored under their headword, so every form of a word lands on one row -
/// "une randonnée" and "la randonnée" are one word met twice, not two words. The encounter
/// is what records the second meeting, and its identifier is built from the source term
/// rather than the headword so that the two forms stay distinguishable. Re-importing the
/// same file produces the same identifiers and therefore no new encounters.
///
/// Nothing is fetched from a dictionary here. Words arrive as bare headwords and are filled
/// in later by the existing parse-on-export path, which is what keeps an import of several
/// hundred terms down to the time it takes to write them.
/// </remarks>
public record SaveVocabularyTermsCommand : IRequest<SaveVocabularyTermsResult>
{
    public required IReadOnlyList<ImportedTerm> Terms { get; init; }
    public Language Language { get; init; } = Language.French;
    public WordEncounterSource Source { get; init; } = WordEncounterSource.ImportedFile;

    /// <summary>
    /// Names the import the encounters belong to - a list name, or a hash of the content.
    /// Encounter identifiers are prefixed with it, which is what makes a re-import idempotent.
    /// </summary>
    public required string SourceIdentifierBase { get; init; }

    public string? Context { get; init; }
}

public class SaveVocabularyTermsCommandHandler
    : IRequestHandler<SaveVocabularyTermsCommand, SaveVocabularyTermsResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public SaveVocabularyTermsCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<SaveVocabularyTermsResult> Handle(
        SaveVocabularyTermsCommand request,
        CancellationToken cancellationToken)
    {
        var result = new SaveVocabularyTermsResult();

        if (request.Terms.Count == 0)
        {
            return result;
        }

        // Counted rather than inferred: UpsertWord reports neither whether it created the
        // word nor whether the encounter was new, and on a re-import both are often false
        var wordsBefore = await _context.Words
            .CountAsync(w => w.Language == request.Language, cancellationToken);

        var prefix = request.SourceIdentifierBase + ":";

        var encountersBefore = await _context.WordEncounters
            .CountAsync(
                we => we.Source == request.Source && we.SourceIdentifier!.StartsWith(prefix),
                cancellationToken);

        foreach (var term in request.Terms)
        {
            await _sender.Send(new UpsertWordCommand
            {
                Headword = term.Lemma,
                Language = request.Language,
                Source = request.Source,
                SourceIdentifier = prefix + term.SourceTerm,
                Context = request.Context,
                Notes = term.Notes
            }, cancellationToken);
        }

        var wordsAfter = await _context.Words
            .CountAsync(w => w.Language == request.Language, cancellationToken);

        var encountersAfter = await _context.WordEncounters
            .CountAsync(
                we => we.Source == request.Source && we.SourceIdentifier!.StartsWith(prefix),
                cancellationToken);

        result.WordsCreated = wordsAfter - wordsBefore;
        result.EncountersCreated = encountersAfter - encountersBefore;
        result.Lemmas = request.Terms
            .Select(t => t.Lemma)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return result;
    }
}
