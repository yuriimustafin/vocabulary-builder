using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Entities;
using VocabularyBuilder.Domain.Entities.ImportedBook;

namespace VocabularyBuilder.Application.ImportWords.Commands;

// Later if we will have more than 1 source of book words - this class can become a context for strategies
public record ImportBookWordsCommand(string FilePath) : IRequest<int>;
public class ImportBookWordsCommandHandler : IRequestHandler<ImportBookWordsCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly IBookImportParser _bookParser;

    public ImportBookWordsCommandHandler(IApplicationDbContext context, IBookImportParser bookParser)
    {
        _context = context;
        _bookParser = bookParser;
    }

    public async Task<int> Handle(ImportBookWordsCommand request, CancellationToken cancellationToken)
    {
        var filepath = request.FilePath
                .Trim()
                .Trim('"', '\'');
        try
        {
            string fileContent = File.ReadAllText(filepath);
            var importingWords = await _bookParser.GetWords(fileContent);
            var storedWords = _context.ImportedBookWords
                    .Select(x => new { Word = x.Headword, x.Page });

            // TODO: Improve it later and take BookInfo into consideration. Investigate what query will be generated here. 
            importingWords = importingWords
                .Where(iw => !storedWords
                    .Any(sw => sw.Word == iw.Headword && sw.Page == iw.Page))
                .ToList();

            _context.ImportedBookWords.AddRange(importingWords);
            await _context.SaveChangesAsync(cancellationToken);

            return importingWords.Count();
        }
        catch (IOException e)
        {
            Console.WriteLine($"An error occurred while reading the file: {e.Message}");
        }
        return 0;
    }

}
