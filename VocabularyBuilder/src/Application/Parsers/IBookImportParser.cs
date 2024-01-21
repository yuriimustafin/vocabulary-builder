using VocabularyBuilder.Domain.Entities.ImportedBook;

namespace VocabularyBuilder.Application.Parsers;
public interface IBookImportParser
{
    Task<IList<ImportedBookWord>> GetWords(string htmlContent);
}
