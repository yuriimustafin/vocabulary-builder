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

public record ImportBookWordsCommand(string FileContent) : IRequest<int>;

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

        var uniqueHeadwords = importedWords
            .Select(w => w.TrimmedHeadword())
            .Distinct()
            .ToList();

        var lookupResults = await _sender.Send(new LookupWordsFromDictionaryQuery
        {
            Words = uniqueHeadwords,
            SourceType = DictionarySourceType.Oxford
        }, cancellationToken);
        
        // Map from the original searched term to the lookup result
        // Each result now explicitly tracks what was searched, handling "does" -> "do" redirects
        var lookupMap = lookupResults.ToDictionary(
            lr => lr.SearchedTerm,
            lr => lr
        );

        return await ImportWords(importedWords, lookupMap, cancellationToken);
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
        Dictionary<string, WordLookupResult> lookupMap,
        CancellationToken cancellationToken)
    {
        var importedCount = 0;

        foreach (var importedWord in importedWords)
        {
            var upsertCommand = BuildUpsertCommand(importedWord, lookupMap);
            await _sender.Send(upsertCommand, cancellationToken);
            importedCount++;
        }

        return importedCount;
    }

    private UpsertWordCommand BuildUpsertCommand(
        ImportedBookWord importedWord,
        Dictionary<string, WordLookupResult> lookupMap)
    {
        var trimmedHeadword = importedWord.TrimmedHeadword();
        
        if (lookupMap.TryGetValue(trimmedHeadword, out var lookupResult))
        {
            return CreateUpsertCommandFromLookup(importedWord, lookupResult);
        }
        
        return CreateUpsertCommandFromImportedWord(importedWord, trimmedHeadword);
    }

    private UpsertWordCommand CreateUpsertCommandFromLookup(
        ImportedBookWord importedWord,
        WordLookupResult lookupResult)
    {
        return new UpsertWordCommand
        {
            Headword = lookupResult.Word.Headword,
            Transcription = lookupResult.Word.Transcription,
            PartOfSpeech = lookupResult.Word.PartOfSpeech,
            Frequency = lookupResult.Word.Frequency,
            Examples = lookupResult.Word.Examples?.ToList(),
            Senses = lookupResult.Word.Senses?.ToList(),
            Source = WordEncounterSource.KindleHighlights,
            SourceIdentifier = BuildSourceIdentifier(importedWord, lookupResult.Word.Headword),
            Context = importedWord.Book?.Title,
            Notes = importedWord.Note,
            DictionarySources = lookupResult.DictionarySources.Any() 
                ? lookupResult.DictionarySources 
                : null
        };
    }

    private UpsertWordCommand CreateUpsertCommandFromImportedWord(
        ImportedBookWord importedWord,
        string trimmedHeadword)
    {
        Console.WriteLine($"No dictionary result for '{importedWord.Headword}', using trimmed form: {trimmedHeadword}");
        
        return new UpsertWordCommand
        {
            Headword = trimmedHeadword,
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
