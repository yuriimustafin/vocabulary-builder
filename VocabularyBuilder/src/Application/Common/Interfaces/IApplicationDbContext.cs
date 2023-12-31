using VocabularyBuilder.Domain.Entities;

namespace VocabularyBuilder.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TodoList> TodoLists { get; }

    DbSet<TodoItem> TodoItems { get; }

    DbSet<Word> Words { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
