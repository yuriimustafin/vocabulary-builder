using System.Text.RegularExpressions;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Imports.Commands;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;

namespace VocabularyBuilder.Application.ImportWords.Commands;

public record ImportBookWordsCommand(string FileContent, Language Language = Language.English, string? FileName = null)
    : IRequest<ImportBookWordsResult>;

public class ImportBookWordsResult
{
    public int WordsImported { get; set; }

    /// <summary>The record of this import, for the Imports page.</summary>
    public int? ImportId { get; set; }
}

public class ImportBookWordsCommandHandler : IRequestHandler<ImportBookWordsCommand, ImportBookWordsResult>
{
    private readonly IBookImportParser _bookParser;
    private readonly ISender _sender;

    public ImportBookWordsCommandHandler(IBookImportParser bookParser, ISender sender)
    {
        _bookParser = bookParser;
        _sender = sender;
    }

    public Task<ImportBookWordsResult> Handle(ImportBookWordsCommand request, CancellationToken cancellationToken)
    {
        return ImportRun.TrackAsync(_sender, new StartImportCommand
        {
            Kind = ImportKind.Kindle,
            Language = request.Language,
            FileName = request.FileName
        }, importId => Import(request, importId, cancellationToken), cancellationToken);
    }

    private async Task<ImportBookWordsResult> Import(
        ImportBookWordsCommand request, int importId, CancellationToken cancellationToken)
    {
        var result = new ImportBookWordsResult { ImportId = importId > 0 ? importId : null };

        var importedWords = await ParseKindleHtml(request.FileContent);
        if (importedWords == null || !importedWords.Any())
        {
            await _sender.Send(new CompleteImportCommand(importId, 0), cancellationToken);
            return result;
        }

        // Don't parse from dictionary - just store the headwords
        // They will be parsed later during export
        result.WordsImported = await ImportWords(importedWords, importId, cancellationToken);

        var bookTitle = importedWords.Select(w => w.Book?.Title).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

        await _sender.Send(new CompleteImportCommand(importId, importedWords.Count, Name: bookTitle), cancellationToken);

        return result;
    }

    private async Task<IList<ImportedBookWord>?> ParseKindleHtml(string htmlContent)
    {
        try
        {
            return await _bookParser.GetWords(htmlContent);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error parsing Kindle HTML: {e.Message}");
            return null;
        }
    }

    private async Task<int> ImportWords(
        IList<ImportedBookWord> importedWords,
        int importId,
        CancellationToken cancellationToken)
    {
        var importedCount = 0;

        foreach (var importedWord in importedWords)
        {
            var upsertCommand = BuildUpsertCommand(importedWord, importId);
            await _sender.Send(upsertCommand, cancellationToken);
            importedCount++;
        }

        return importedCount;
    }

    private UpsertWordCommand BuildUpsertCommand(ImportedBookWord importedWord, int importId)
    {
        var trimmedHeadword = importedWord.TrimmedHeadword();

        return new UpsertWordCommand
        {
            Headword = trimmedHeadword,
            Language = importedWord.Language,
            // No dictionary data yet - will be parsed on export
            Source = WordEncounterSource.KindleHighlights,
            SourceIdentifier = BuildSourceIdentifier(importedWord, trimmedHeadword),
            Context = importedWord.Book?.Title,
            Notes = importedWord.Note,
            ImportId = importId,
            ImportSourceTerm = importedWord.Headword,
            // A highlight is the word exactly as the book printed it
            EncounterForm = trimmedHeadword
        };
    }

    private string BuildSourceIdentifier(ImportedBookWord importedWord, string normalizedHeadword)
    {
        var bookTitle = importedWord.Book?.Title ?? "Unknown";
        var pageNumber = StringHelper.ExtractPageNumber(importedWord.Heading);
        return $"{bookTitle}:{normalizedHeadword}:{pageNumber}";
    }
}
