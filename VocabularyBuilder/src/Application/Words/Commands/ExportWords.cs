using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Commands;

public record ExportWordsCommand(
    List<int> WordIds) : IRequest<ExportWordsResult>;

public record ExportWordsResult(
    string CsvContent,
    int ExportedCount);

public class ExportWordsCommandHandler : IRequestHandler<ExportWordsCommand, ExportWordsResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IWordsExporter _wordsExporter;
    private readonly ISender _sender;

    public ExportWordsCommandHandler(
        IApplicationDbContext context,
        IWordsExporter wordsExporter,
        ISender sender)
    {
        _context = context;
        _wordsExporter = wordsExporter;
        _sender = sender;
    }

    public async Task<ExportWordsResult> Handle(ExportWordsCommand request, CancellationToken cancellationToken)
    {
        // Fetch words with all necessary data
        var words = await _context.Words
            .Include(w => w.Senses!)
            .Include(w => w.DictionarySources)
            .Where(w => request.WordIds.Contains(w.Id))
            .ToListAsync(cancellationToken);

        if (!words.Any())
        {
            return new ExportWordsResult(string.Empty, 0);
        }

        // Parse unparsed words (those without Senses)
        var unparsedWords = words.Where(w => w.Senses == null || !w.Senses.Any()).ToList();
        if (unparsedWords.Any())
        {
            Console.WriteLine($"Parsing {unparsedWords.Count} words that don't have definitions yet...");
            
            var headwordsToLookup = unparsedWords.Select(w => w.Headword).ToList();
            var lookupResults = await _sender.Send(new LookupWordsFromDictionaryQuery
            {
                Words = headwordsToLookup,
                SourceType = DictionarySourceType.Oxford
            }, cancellationToken);
            
            // Update words with parsed data using UpsertWordCommand
            foreach (var lookupResult in lookupResults)
            {
                var wordToUpdate = unparsedWords.FirstOrDefault(w => 
                    w.Headword.Equals(lookupResult.Word.Headword, StringComparison.OrdinalIgnoreCase) ||
                    w.Headword.Equals(lookupResult.SearchedTerm, StringComparison.OrdinalIgnoreCase));
                
                if (wordToUpdate != null)
                {
                    Console.WriteLine($"Updating word: {wordToUpdate.Headword} with parsed data");
                    
                    // Get the original encounter info to preserve it
                    // Load to memory first, then order (SQLite doesn't support OrderBy on DateTimeOffset)
                    var encounters = await _context.WordEncounters
                        .Where(we => we.WordId == wordToUpdate.Id)
                        .ToListAsync(cancellationToken);
                    var firstEncounter = encounters.OrderBy(we => we.Created).FirstOrDefault();
                    
                    // Use UpsertWordCommand to handle all the update logic
                    await _sender.Send(new UpsertWordCommand
                    {
                        Headword = lookupResult.Word.Headword,
                        Transcription = lookupResult.Word.Transcription,
                        PartOfSpeech = lookupResult.Word.PartOfSpeech,
                        Frequency = lookupResult.Word.Frequency,
                        Examples = lookupResult.Word.Examples?.ToList(),
                        Senses = lookupResult.Word.Senses?.ToList(),
                        Source = firstEncounter?.Source ?? WordEncounterSource.Manual,
                        SourceIdentifier = firstEncounter?.SourceIdentifier ?? $"export-{DateTime.UtcNow:yyyy-MM-dd}",
                        Context = firstEncounter?.Context ?? "Dictionary parsing on export",
                        DictionarySources = lookupResult.DictionarySources.Any() ? lookupResult.DictionarySources : null
                    }, cancellationToken);
                }
            }
            
            // Refresh words from database to get updated data
            words = await _context.Words
                .Include(w => w.Senses!)
                .Include(w => w.DictionarySources)
                .Where(w => request.WordIds.Contains(w.Id))
                .ToListAsync(cancellationToken);
        }

        // Update status and generate SyncId before exporting (so it's included in CSV)
        foreach (var word in words)
        {
            word.Status = WordStatus.Exported;
            // Generate SyncId only if it doesn't exist yet
            if (!word.SyncId.HasValue)
            {
                word.SyncId = Guid.NewGuid();
            }
        }

        // Generate CSV content (includes SyncId)
        var csvContent = _wordsExporter.ExportWords(words);

        // Save changes to database
        await _context.SaveChangesAsync(cancellationToken);

        return new ExportWordsResult(csvContent, words.Count);
    }
}
