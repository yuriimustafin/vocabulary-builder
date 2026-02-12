using System.Text.RegularExpressions;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;

namespace VocabularyBuilder.Application.ImportWords.Commands;

// Later if we will have more than 1 source of book words - this class can become a context for strategies
public record ImportBookWordsCommand(string FileContent) : IRequest<int>;
public class ImportBookWordsCommandHandler : IRequestHandler<ImportBookWordsCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IBookImportParser _bookParser;
    private readonly IMediator _mediator;

    public ImportBookWordsCommandHandler(IApplicationDbContext context, IBookImportParser bookParser, IMediator mediator)
    {
        _context = context;
        _bookParser = bookParser;
        _mediator = mediator;
    }

    // TODO: Consider using Notifications or IPipelineBehavior
    public async Task<int> Handle(ImportBookWordsCommand request, CancellationToken cancellationToken)
    {
        var importingWords = await GetWordsFromContent(request);
        Console.WriteLine("GetWordsFromContent");

        await SaveNewWordsToDb(importingWords, cancellationToken);
        Console.WriteLine("SaveNewWordsToDb");

        await GenerateWordsInDb(cancellationToken);
        Console.WriteLine("GenerateWordsInDb");

        await AddFrequency(cancellationToken);
        Console.WriteLine("AddFrequency");

        return importingWords is not null ? importingWords.Count() : 0;
    }

    private async Task<IList<ImportedBookWord>?> GetWordsFromContent(ImportBookWordsCommand request)
    {
        IList<ImportedBookWord>? importingWords = null;
        try
        {
            importingWords = await _bookParser.GetWords(request.FileContent);
        }
        catch (Exception e)
        {
            Console.WriteLine($"An error occurred while parsing the file content: {e.Message}");
        }
        return importingWords;
    }

    private async Task SaveNewWordsToDb(IList<ImportedBookWord>? importingWords, CancellationToken cancellationToken)
    {
        if (importingWords is null)
            return;
        var storedWords = _context.ImportedBookWords
                    .AsEnumerable()
                    .Select(x => new { Word = x.Headword, Page = x.ExtractDigitsFromHeading() });

        // TODO: Improve it later and take BookInfo into consideration. Investigate what query will be generated here. 
        importingWords = importingWords
            .AsEnumerable()
            .Where(iw => !storedWords
            // TODO: Override Equals for ImportedBookWord
                .Any(sw => sw.Word == iw.Headword && sw.Page == iw.ExtractDigitsFromHeading()))
            .ToList();

        _context.ImportedBookWords.AddRange(importingWords);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task GenerateWordsInDb(CancellationToken cancellationToken)
    {
        var addedImportedWords = _context.ImportedBookWords
                .Where(x => x.Status == ImportWordStatus.Added)
                .ToList();

        var existingWordsDict = await GetExistingWordsDictionary(addedImportedWords, cancellationToken);
        var (newWords, wordEncounters) = ProcessImportedWords(addedImportedWords, existingWordsDict);

        await SaveNewWords(newWords, cancellationToken);
        AddEncountersForNewWords(newWords, addedImportedWords, wordEncounters);
        await SaveUniqueEncounters(wordEncounters, cancellationToken);
    }

    private async Task<Dictionary<string, Word>> GetExistingWordsDictionary(
        List<ImportedBookWord> importedWords, 
        CancellationToken cancellationToken)
    {
        var trimmedImportedWords = importedWords.Select(x => x.TrimmedHeadword());
        
        return await _context.Words
            .Where(w => trimmedImportedWords.Contains(w.Headword))
            .GroupBy(w => w.Headword)
            .Select(grp => grp.First())
            .ToDictionaryAsync(w => w.Headword, w => w, cancellationToken);
    }

    private (List<Word> newWords, List<WordEncounter> encounters) ProcessImportedWords(
        List<ImportedBookWord> importedWords,
        Dictionary<string, Word> existingWordsDict)
    {
        var newWords = new List<Word>();
        var wordEncounters = new List<WordEncounter>();

        foreach (var importedWord in importedWords)
        {
            var trimmedHeadWord = importedWord.TrimmedHeadword();
            var sourceIdentifier = importedWord.GetUniqueSourceIdentifier();
            
            if (existingWordsDict.ContainsKey(trimmedHeadWord))
            {
                var existingWord = existingWordsDict[trimmedHeadWord];
                importedWord.Word = existingWord;
                
                wordEncounters.Add(CreateEncounter(
                    existingWord.Id, 
                    sourceIdentifier, 
                    importedWord.Book?.Title, 
                    importedWord.Note));
            }
            else
            {
                var newWord = new Word { Headword = trimmedHeadWord };
                importedWord.Word = newWord;
                existingWordsDict.Add(newWord.Headword, newWord);
                newWords.Add(newWord);
            }
            
            importedWord.Status = ImportWordStatus.Processed;
        }

        return (newWords, wordEncounters);
    }

    private WordEncounter CreateEncounter(int wordId, string sourceIdentifier, string? context, string? notes)
    {
        return new WordEncounter
        {
            WordId = wordId,
            Source = WordEncounterSource.KindleHighlights,
            SourceIdentifier = sourceIdentifier,
            Context = context,
            Notes = notes
        };
    }

    private async Task SaveNewWords(List<Word> newWords, CancellationToken cancellationToken)
    {
        if (newWords.Any())
        {
            _context.Words.AddRange(newWords);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private void AddEncountersForNewWords(
        List<Word> newWords,
        List<ImportedBookWord> importedWords,
        List<WordEncounter> wordEncounters)
    {
        foreach (var newWord in newWords)
        {
            var importedWord = importedWords.First(iw => iw.Word == newWord);
            var sourceIdentifier = importedWord.GetUniqueSourceIdentifier();
            
            wordEncounters.Add(CreateEncounter(
                newWord.Id,
                sourceIdentifier,
                importedWord.Book?.Title,
                importedWord.Note));
        }
    }

    private async Task SaveUniqueEncounters(
        List<WordEncounter> wordEncounters,
        CancellationToken cancellationToken)
    {
        var uniqueEncounters = new List<WordEncounter>();
        
        foreach (var encounter in wordEncounters)
        {
            if (!string.IsNullOrEmpty(encounter.SourceIdentifier))
            {
                var exists = await _context.WordEncounters
                    .AnyAsync(we => 
                        we.WordId == encounter.WordId && 
                        we.SourceIdentifier == encounter.SourceIdentifier &&
                        we.Source == encounter.Source, 
                        cancellationToken);
                
                if (!exists)
                {
                    uniqueEncounters.Add(encounter);
                }
            }
            else
            {
                uniqueEncounters.Add(encounter);
            }
        }

        if (uniqueEncounters.Any())
        {
            _context.WordEncounters.AddRange(uniqueEncounters);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task AddFrequency(CancellationToken cancellationToken)
    {
        var wordsForAddingFrequency = _context.Words.Where(x => x.Frequency == null);

        // TODO: Remove hardcode!
        var filePath = "D:\\__Education\\English\\lemma.en.txt";
        try
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string? line;
                int cnt = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    var foundWords = wordsForAddingFrequency
                            .Where(x => x.Frequency == null && line.Contains(x.Headword));


                    if (foundWords.Count() > 0)
                    {
                        foreach (var word in foundWords)
                        {
                            if (ContainsWholeWord(line, word.Headword))
                            {
                                word.Frequency = StringHelper.ExtractDigits(line);
                            }
                        }
                    }
                    if (cnt % 1000 == 0)
                    {
                        Console.WriteLine("cnt: " + cnt);
                    }
                    cnt++;
                }
            }
        }
        catch (IOException e)
        {
            Console.WriteLine($"An error occurred while reading the file: {e.Message}");
        }

        foreach (var unchangedWord in wordsForAddingFrequency.Where(x => x.Frequency == null))
        {
            unchangedWord.Frequency ??= -1;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private bool ContainsWholeWord(string input, string word)
    {
        string pattern = $@"\b{Regex.Escape(word)}\b";
        return Regex.IsMatch(input, pattern);
    }
}
