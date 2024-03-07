using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;

namespace VocabularyBuilder.Application.Parsers;
public interface IBookImportParser
{
    Task<IList<ImportedBookWord>> GetWords(string htmlContent);
}
