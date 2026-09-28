using System.Security.Cryptography;
using System.Text;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Application.ImportWords.Commands;

/// <summary>
/// Imports vocabulary from notes written during a lesson.
/// </summary>
/// <remarks>
/// Lesson notes have no format. An item may stand alone or be written against its
/// translation ("un pas=step", "migrer=to migrate"), and a line may hold several items
/// divided by commas, which have to be told apart from the commas inside one. That
/// reading is the model's job; everything after it is the same path a LingQ export takes,
/// so notes and exports produce the same kind of entry. The translations in the notes are
/// read only to be discarded - the word's content comes from the dictionary.
/// </remarks>
public record ImportLessonNotesCommand : IRequest<VocabularyImportResult>
{
    public required string Notes { get; init; }
    public Language Language { get; init; } = Language.French;

    /// <summary>
    /// Names the lesson for idempotency, and labels the encounters it produces. Without
    /// one, a hash of the notes is used.
    /// </summary>
    public string? ListName { get; init; }

    /// <summary>
    /// Applied to every word the import brings in. Several can be given at once, separated
    /// by commas, and a word that already carries tags keeps them.
    /// </summary>
    public string? Tag { get; init; }
}

public class ImportLessonNotesCommandHandler
    : IRequestHandler<ImportLessonNotesCommand, VocabularyImportResult>
{
    private readonly IVocabularyAnalyzer _analyzer;
    private readonly ISender _sender;

    public ImportLessonNotesCommandHandler(IVocabularyAnalyzer analyzer, ISender sender)
    {
        _analyzer = analyzer;
        _sender = sender;
    }

    public async Task<VocabularyImportResult> Handle(
        ImportLessonNotesCommand request,
        CancellationToken cancellationToken)
    {
        var items = await _analyzer.ExtractItemsAsync(request.Notes, request.Language, cancellationToken);

        var result = new VocabularyImportResult { TermsRead = items.Count };

        if (items.Count == 0)
        {
            return result;
        }

        var resolution = await _sender.Send(new ResolveVocabularyTermsQuery
        {
            Terms = items,
            Language = request.Language
        }, cancellationToken);

        result.Skipped = resolution.Skipped;

        var terms = resolution.Resolved
            .Select(r => new ImportedTerm(r.SourceTerm, r.Lemma))
            .ToList();

        var saved = await _sender.Send(new SaveVocabularyTermsCommand
        {
            Terms = terms,
            Language = request.Language,
            Source = WordEncounterSource.LessonNotes,
            SourceIdentifierBase = !string.IsNullOrWhiteSpace(request.ListName)
                ? request.ListName!
                : ComputeHash(request.Notes),
            Context = !string.IsNullOrWhiteSpace(request.ListName) ? request.ListName : "Lesson notes",
            Tags = WordTags.Parse(request.Tag)
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
        return "notes-" + Convert.ToHexString(hashBytes)[..16];
    }
}
