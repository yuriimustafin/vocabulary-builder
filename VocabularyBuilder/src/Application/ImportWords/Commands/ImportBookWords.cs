using System.Text.RegularExpressions;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;

namespace VocabularyBuilder.Application.ImportWords.Commands;

public record ImportBookWordsCommand(string FileContent, Language Language = Language.English) : IRequest<int>;

public class ImportBookWordsCommandHandler : IRequestHandler<ImportBookWordsCommand, int>
{
    private readonly IBookImportParser _bookParser;
    private readonly ISender _sender;

    public ImportBookWordsCommandHandler(IBookImportParser bookParser, ISender sender)
    {
        _bookParser = bookParser;
        _sender = sender;
    }

    public async Task<int> Handle(ImportBookWordsCommand request, CancellationToken cancellationToken)
    {
        var importedWords = await ParseKindleHtml(request.FileContent);
        if (importedWords == null || !importedWords.Any())
            return 0;

        // Don't parse from dictionary - just store the headwords
        // They will be parsed later during export
        return await ImportWords(importedWords, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var importedCount = 0;

        foreach (var importedWord in importedWords)
        {
            var upsertCommand = BuildUpsertCommand(importedWord);
            await _sender.Send(upsertCommand, cancellationToken);
            importedCount++;
        }

        return importedCount;
    }

    private UpsertWordCommand BuildUpsertCommand(ImportedBookWord importedWord)
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
            Notes = importedWord.Note
        };
    }

    private string BuildSourceIdentifier(ImportedBookWord importedWord, string normalizedHeadword)
    {
        var bookTitle = importedWord.Book?.Title ?? "Unknown";
        var pageNumber = StringHelper.ExtractPageNumber(importedWord.Heading);
        return $"{bookTitle}:{normalizedHeadword}:{pageNumber}";
    }
}
