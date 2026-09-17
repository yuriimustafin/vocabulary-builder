using System.Security.Cryptography;
using System.Text;
using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.ImportWords.Commands;

public class VocabularyImportResult
{
    /// <summary>Terms the source offered, before anything was made of them.</summary>
    public int TermsRead { get; set; }

    /// <summary>Terms that reduced to a headword and were saved.</summary>
    public int TermsImported { get; set; }

    public int WordsCreated { get; set; }
    public int EncountersCreated { get; set; }

    public List<string> Lemmas { get; set; } = new();

    /// <summary>
    /// Terms that were not vocabulary items, each with the reason. Reported rather than
    /// discarded quietly so that anything worth keeping can be added by hand.
    /// </summary>
    public List<SkippedTerm> Skipped { get; set; } = new();
}

/// <summary>
/// Imports a LingQ vocabulary export.
/// </summary>
/// <remarks>
/// LingQ saves what the learner clicked, which is a mix of vocabulary and whatever sentence
/// it sat in. The terms are reduced to headwords where they are words and set aside where
/// they are prose, and the learner's own translations are not carried over: an imported
/// word takes its content from the dictionary like any other.
/// </remarks>
public record ImportLingQWordsCommand : IRequest<VocabularyImportResult>
{
    public required string FileContent { get; init; }
    public Language Language { get; init; } = Language.French;

    /// <summary>
    /// Names the import for idempotency. Without one, a hash of the file is used, so
    /// re-importing the same export adds no encounters while a later one that has grown
    /// adds only its new rows.
    /// </summary>
    public string? ListName { get; init; }
}

public class ImportLingQWordsCommandHandler
    : IRequestHandler<ImportLingQWordsCommand, VocabularyImportResult>
{
    private readonly ISender _sender;

    public ImportLingQWordsCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<VocabularyImportResult> Handle(
        ImportLingQWordsCommand request,
        CancellationToken cancellationToken)
    {
        var rows = LingQCsvReader.Read(request.FileContent);

        var result = new VocabularyImportResult { TermsRead = rows.Count };

        if (rows.Count == 0)
        {
            return result;
        }

        var resolution = await _sender.Send(new ResolveVocabularyTermsQuery
        {
            Terms = rows.Select(r => r.Term).ToList(),
            Language = request.Language
        }, cancellationToken);

        result.Skipped = resolution.Skipped;

        // The sentence LingQ recorded is context for the encounter, not content for the
        // word, so it rides along on the encounter and never becomes a definition
        var phrases = rows
            .Where(r => r.Phrase != null)
            .GroupBy(r => r.Term, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Phrase, StringComparer.OrdinalIgnoreCase);

        var terms = resolution.Resolved
            .Select(r => new ImportedTerm(
                r.SourceTerm,
                r.Lemma,
                phrases.GetValueOrDefault(r.SourceTerm)))
            .ToList();

        var saved = await _sender.Send(new SaveVocabularyTermsCommand
        {
            Terms = terms,
            Language = request.Language,
            Source = WordEncounterSource.LingQ,
            SourceIdentifierBase = !string.IsNullOrWhiteSpace(request.ListName)
                ? request.ListName!
                : ComputeHash(request.FileContent),
            Context = !string.IsNullOrWhiteSpace(request.ListName) ? request.ListName : "LingQ import"
        }, cancellationToken);

        result.TermsImported = terms.Count;
        result.WordsCreated = saved.WordsCreated;
        result.EncountersCreated = saved.EncountersCreated;
        result.Lemmas = saved.Lemmas;

        return result;
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return "lingq-" + Convert.ToHexString(hashBytes)[..16];
    }
}
