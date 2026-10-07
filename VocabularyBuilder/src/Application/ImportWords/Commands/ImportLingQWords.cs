using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Application.Imports.Commands;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;

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

    /// <summary>The record of this import, for the Imports page.</summary>
    public int? ImportId { get; set; }
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
    /// Names the import for idempotency. Without one, every LingQ import shares one name:
    /// an export is the learner's whole saved vocabulary rather than one lesson's, so
    /// re-importing the same export adds no encounters while a later one that has grown
    /// adds only its new rows.
    /// </summary>
    public string? ListName { get; init; }

    /// <summary>
    /// Applied to every word the import brings in. Several can be given at once, separated
    /// by commas, and a word that already carries tags keeps them.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>The uploaded file's name, kept on the import's record.</summary>
    public string? FileName { get; init; }
}

public class ImportLingQWordsCommandHandler
    : IRequestHandler<ImportLingQWordsCommand, VocabularyImportResult>
{
    private readonly ISender _sender;

    public ImportLingQWordsCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public Task<VocabularyImportResult> Handle(
        ImportLingQWordsCommand request,
        CancellationToken cancellationToken)
    {
        var sourceIdentifierBase = !string.IsNullOrWhiteSpace(request.ListName)
            ? request.ListName!
            : "lingq";

        return ImportRun.TrackAsync(_sender, new StartImportCommand
        {
            Kind = ImportKind.LingQ,
            Language = request.Language,
            Name = request.ListName,
            FileName = request.FileName,
            SourceIdentifierBase = sourceIdentifierBase,
            Tags = WordTags.Parse(request.Tag)
        }, importId => Import(request, importId, sourceIdentifierBase, cancellationToken), cancellationToken);
    }

    private async Task<VocabularyImportResult> Import(
        ImportLingQWordsCommand request,
        int importId,
        string sourceIdentifierBase,
        CancellationToken cancellationToken)
    {
        var rows = LingQCsvReader.Read(request.FileContent);

        var result = new VocabularyImportResult { TermsRead = rows.Count, ImportId = importId > 0 ? importId : null };

        if (rows.Count == 0)
        {
            await _sender.Send(new CompleteImportCommand(importId, 0), cancellationToken);
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
                phrases.GetValueOrDefault(r.SourceTerm),
                r.Form,
                // The phrase is the sentence the word was read in, which makes it the best
                // example there is for the form it was read in
                phrases.GetValueOrDefault(r.SourceTerm)))
            .ToList();

        var saved = await _sender.Send(new SaveVocabularyTermsCommand
        {
            Terms = terms,
            Language = request.Language,
            Source = WordEncounterSource.LingQ,
            SourceIdentifierBase = sourceIdentifierBase,
            Context = !string.IsNullOrWhiteSpace(request.ListName) ? request.ListName : "LingQ import",
            Tags = WordTags.Parse(request.Tag),
            ImportId = importId
        }, cancellationToken);

        result.TermsImported = terms.Count;
        result.WordsCreated = saved.WordsCreated;
        result.EncountersCreated = saved.EncountersCreated;
        result.Lemmas = saved.Lemmas;

        await _sender.Send(new CompleteImportCommand(importId, rows.Count, resolution.Skipped), cancellationToken);

        return result;
    }
}
