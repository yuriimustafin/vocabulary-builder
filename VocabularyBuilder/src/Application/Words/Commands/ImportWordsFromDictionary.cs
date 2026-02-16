using System.Security.Cryptography;
using System.Text;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Commands;

public record ImportWordsFromDictionaryCommand : IRequest<ImportWordsFromDictionaryResult>
{
    public required List<string> Words { get; init; }
    public string? ListName { get; init; }
    public DictionarySourceType SourceType { get; init; } = DictionarySourceType.Oxford;
}

public class ImportWordsFromDictionaryResult
{
    public int WordsImported { get; set; }
    public int EncountersCreated { get; set; }
    public List<string> ImportedWords { get; set; } = new();
}

public class ImportWordsFromDictionaryCommandHandler : IRequestHandler<ImportWordsFromDictionaryCommand, ImportWordsFromDictionaryResult>
{
    private readonly ISender _sender;

    public ImportWordsFromDictionaryCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<ImportWordsFromDictionaryResult> Handle(ImportWordsFromDictionaryCommand request, CancellationToken cancellationToken)
    {
        // Lookup words from dictionary (with caching)
        var lookupResults = await _sender.Send(new LookupWordsFromDictionaryQuery
        {
            Words = request.Words,
            SourceType = request.SourceType
        }, cancellationToken);

        // Generate source identifier: use listName if provided, otherwise hash of the word list
        var wordListContent = string.Join("\n", request.Words);
        var sourceIdentifierBase = !string.IsNullOrWhiteSpace(request.ListName) 
            ? request.ListName 
            : ComputeListHash(wordListContent);

        var result = new ImportWordsFromDictionaryResult();
        
        // Save words to the database
        foreach (var lookupResult in lookupResults)
        {
            var wordId = await _sender.Send(new UpsertWordCommand
            {
                Headword = lookupResult.Word.Headword,
                Transcription = lookupResult.Word.Transcription,
                PartOfSpeech = lookupResult.Word.PartOfSpeech,
                Frequency = lookupResult.Word.Frequency,
                Examples = lookupResult.Word.Examples?.ToList(),
                Senses = lookupResult.Word.Senses?.ToList(),
                Source = WordEncounterSource.OxfordDictionaryList,
                SourceIdentifier = $"{sourceIdentifierBase}:{lookupResult.Word.Headword}",
                Context = !string.IsNullOrWhiteSpace(request.ListName) ? request.ListName : "Oxford Dictionary Import",
                DictionarySources = lookupResult.DictionarySources.Any() ? lookupResult.DictionarySources : null
            }, cancellationToken);
            
            result.ImportedWords.Add(lookupResult.Word.Headword);
        }

        result.WordsImported = lookupResults.Count;
        result.EncountersCreated = lookupResults.Count; // Note: Actual count may be less due to idempotency

        return result;
    }

    private static string ComputeListHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes)[..16]; // Use first 16 chars for readability
    }
}
