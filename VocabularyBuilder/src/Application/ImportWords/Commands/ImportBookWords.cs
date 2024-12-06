using System.Text.RegularExpressions;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;

namespace VocabularyBuilder.Application.ImportWords.Commands;

// Later if we will have more than 1 source of book words - this class can become a context for strategies
public record ImportBookWordsCommand(string FilePath) : IRequest<int>;
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
        var importingWords = await GetWordsFromFile(request);
        Console.WriteLine("GetWordsFromFile");

        await SaveNewWordsToDb(importingWords, cancellationToken);
        Console.WriteLine("SaveNewWordsToDb");

        await GenerateWordsInDb(cancellationToken);
        Console.WriteLine("GenerateWordsInDb");

        await AddFrequency(cancellationToken);
        Console.WriteLine("AddFrequency");

        return importingWords is not null ? importingWords.Count() : 0;
    }

    private async Task<IList<ImportedBookWord>?> GetWordsFromFile(ImportBookWordsCommand request)
    {
        var filepath = request.FilePath
                .Trim()
                .Trim('"', '\'');
        IList<ImportedBookWord>? importingWords = null;
        try
        {
            string fileContent = File.ReadAllText(filepath);
            importingWords = await _bookParser.GetWords(fileContent);
        }
        catch (IOException e)
        {
            Console.WriteLine($"An error occurred while reading the file: {e.Message}");
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
                .Where(x => x.Status == ImportWordStatus.Added);
        var trimmedImportedWords = addedImportedWords.AsEnumerable().Select(x => x.TrimmedHeadword());
        var dublicatingWords = _context.Words
                .Where(w => trimmedImportedWords.Contains(w.Headword))
                .GroupBy(w => w.Headword)
                .Select(grp => grp.First())
                // from biz logic it is more immportant to get here distinct headwords than rely on IDs
                .ToDictionary(w => w.Headword, w => w);
        var newWords = new List<Word>();

        foreach (var importedWord in addedImportedWords)
        {
            var trimmedHeadWord = importedWord.TrimmedHeadword();
            if (dublicatingWords.ContainsKey(trimmedHeadWord))
            {
                var dubWord = dublicatingWords[trimmedHeadWord];
                dubWord.EncounterCount++; 
                importedWord.Word = dubWord;
            }
            else
            {
                var newWord = new Word()
                {
                    Headword = importedWord.TrimmedHeadword(),
                    EncounterCount = 1
                };
                importedWord.Word = newWord;
                dublicatingWords.Add(newWord.Headword, newWord);
                newWords.Add(newWord);
            }
            importedWord.Status = ImportWordStatus.Processed;
        }

        _context.Words.AddRange(newWords);
        await _context.SaveChangesAsync(cancellationToken);

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
