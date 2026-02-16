using Microsoft.EntityFrameworkCore.ChangeTracking;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;

namespace VocabularyBuilder.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TodoList> TodoLists { get; }

    DbSet<TodoItem> TodoItems { get; }

    DbSet<Word> Words { get; }

    DbSet<WordEncounter> WordEncounters { get; }
    
    DbSet<WordDictionarySource> WordDictionarySources { get; }

    DbSet<FrequencyWord> FrequencyWords { get; }

    DbSet<ImportedBookWord> ImportedBookWords { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
