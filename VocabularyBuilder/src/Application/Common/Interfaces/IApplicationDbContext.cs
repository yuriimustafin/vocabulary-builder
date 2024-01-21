using VocabularyBuilder.Domain.Entities;
using VocabularyBuilder.Domain.Entities.ImportedBook;

namespace VocabularyBuilder.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TodoList> TodoLists { get; }

    DbSet<TodoItem> TodoItems { get; }

    DbSet<Word> Words { get; }

    DbSet<ImportedBookWord> ImportedBookWords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
