using System.Security.Cryptography;
using System.Text;
using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Application.Imports.Commands;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Commands;

public record ImportWordsFromDictionaryCommand : IRequest<ImportWordsFromDictionaryResult>
{
    public required List<string> Words { get; init; }
    public string? ListName { get; init; }
    public Language Language { get; init; } = Language.English;
    public DictionarySourceType SourceType { get; init; } = DictionarySourceType.Oxford;

    /// <summary>
    /// If true, parse words from dictionary immediately (for URLs).
    /// If false, defer parsing until export (for text lists).
    /// </summary>
    public bool ParseImmediately { get; init; } = false;
}

public class ImportWordsFromDictionaryResult
{
    public int WordsImported { get; set; }
    public int EncountersCreated { get; set; }
    public List<string> ImportedWords { get; set; } = new();

    /// <summary>The record of this import, for the Imports page.</summary>
    public int? ImportId { get; set; }
}

public class ImportWordsFromDictionaryCommandHandler : IRequestHandler<ImportWordsFromDictionaryCommand, ImportWordsFromDictionaryResult>
{
    private readonly ISender _sender;

    public ImportWordsFromDictionaryCommandHandler(ISender sender)
    {
        _sender = sender;
    }

    public Task<ImportWordsFromDictionaryResult> Handle(ImportWordsFromDictionaryCommand request, CancellationToken cancellationToken)
    {
        // Generate source identifier: use listName if provided, otherwise hash of the word list
        var wordListContent = string.Join("\n", request.Words);
        var sourceIdentifierBase = !string.IsNullOrWhiteSpace(request.ListName)
            ? request.ListName
            : ComputeListHash(wordListContent);

        return ImportRun.TrackAsync(_sender, new StartImportCommand
        {
            Kind = ImportKind.BulkList,
            Language = request.Language,
            Name = request.ListName,
            SourceIdentifierBase = sourceIdentifierBase
        }, importId => Import(request, importId, sourceIdentifierBase, cancellationToken), cancellationToken);
    }

    private async Task<ImportWordsFromDictionaryResult> Import(
        ImportWordsFromDictionaryCommand request,
        int importId,
        string sourceIdentifierBase,
        CancellationToken cancellationToken)
    {
        var result = new ImportWordsFromDictionaryResult { ImportId = importId > 0 ? importId : null };

        if (request.ParseImmediately)
        {
            // Parse from dictionary immediately (for URLs)
            var lookupResults = await _sender.Send(new LookupWordsFromDictionaryQuery
            {
                Words = request.Words,
                Language = request.Language,
                SourceType = request.SourceType
            }, cancellationToken);

            // Save words to the database with full definitions
            foreach (var lookupResult in lookupResults)
            {
                var wordId = await _sender.Send(new UpsertWordCommand
                {
                    Headword = lookupResult.Word.Headword,
                    Language = request.Language,
                    Transcription = lookupResult.Word.Transcription,
                    PartOfSpeech = lookupResult.Word.PartOfSpeech,
                    Gender = lookupResult.Word.Gender,
                    IsPluralOnly = lookupResult.Word.IsPluralOnly,
                    Frequency = lookupResult.Word.Frequency,
                    Examples = lookupResult.Word.Examples?.ToList(),
                    Senses = lookupResult.Word.Senses?.ToList(),
                    Source = WordEncounterSource.OxfordDictionaryList,
                    SourceIdentifier = $"{sourceIdentifierBase}:{lookupResult.Word.Headword}",
                    Context = !string.IsNullOrWhiteSpace(request.ListName)
                        ? request.ListName
                        : $"{request.SourceType} Dictionary Import",
                    DictionarySources = lookupResult.DictionarySources.Any() ? lookupResult.DictionarySources : null,
                    Forms = lookupResult.Forms.Any() ? lookupResult.Forms : null,
                    ImportId = importId,
                    ImportSourceTerm = lookupResult.SearchedTerm
                }, cancellationToken);

                result.ImportedWords.Add(lookupResult.Word.Headword);
            }

            result.WordsImported = lookupResults.Count;
            result.EncountersCreated = lookupResults.Count;

            // A term the dictionary had no entry for would otherwise vanish without a trace
            var answered = lookupResults
                .Select(r => r.SearchedTerm)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var unanswered = request.Words
                .Where(w => !answered.Contains(w))
                .Select(w => new SkippedTerm(w, "Not in dictionary"))
                .ToList();

            await _sender.Send(new CompleteImportCommand(importId, request.Words.Count, unanswered), cancellationToken);
        }
        else
        {
            // Defer parsing - just store headwords (for text lists)
            foreach (var word in request.Words)
            {
                var cleanWord = word.Trim().ToLower();
                if (string.IsNullOrEmpty(cleanWord)) continue;

                var wordId = await _sender.Send(new UpsertWordCommand
                {
                    Headword = cleanWord,
                    Language = request.Language,
                    // No dictionary data yet - will be parsed on export
                    Source = WordEncounterSource.OxfordDictionaryList,
                    SourceIdentifier = $"{sourceIdentifierBase}:{cleanWord}",
                    Context = !string.IsNullOrWhiteSpace(request.ListName) ? request.ListName : "Text Word Import",
                    ImportId = importId,
                    ImportSourceTerm = word.Trim()
                }, cancellationToken);

                result.ImportedWords.Add(cleanWord);
            }

            result.WordsImported = request.Words.Count;
            result.EncountersCreated = request.Words.Count;

            await _sender.Send(new CompleteImportCommand(importId, request.Words.Count), cancellationToken);
        }

        return result;
    }

    private static string ComputeListHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes)[..16]; // Use first 16 chars for readability
    }
}
