using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Entities;

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
        try
        {
            string fileContent = File.ReadAllText(request.FilePath);
            var words = await _bookParser.GetWords(fileContent);

            _context.ImportedBookWords.AddRange(words);
            await _context.SaveChangesAsync(cancellationToken);
            return words.Count();
        }
        catch (IOException e)
        {
            Console.WriteLine($"An error occurred while reading the file: {e.Message}");
        }
        return 0;
    }

}
