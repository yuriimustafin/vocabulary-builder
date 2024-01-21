using VocabularyBuilder.Domain.Entities.ImportedBook;

namespace VocabularyBuilder.Application.Parsers;
public interface IBookImportParser
{
    Task<IEnumerable<ImportedBookWord>> GetWords(string htmlContent);
}
