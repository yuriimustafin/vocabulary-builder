using Microsoft.EntityFrameworkCore.ChangeTracking;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Entities.Study;
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

    DbSet<WordForm> WordForms { get; }

    /// <summary>
    /// Meanings, reachable on their own so that replacing a word's senses can delete the
    /// ones it had. The relationship is optional, so an orphaned sense would otherwise be
    /// left behind with no word rather than removed.
    /// </summary>
    DbSet<Sense> Senses { get; }

    DbSet<FrequencyWord> FrequencyWords { get; }

    DbSet<ImportedBookWord> ImportedBookWords { get; }

    DbSet<VocabularyList> VocabularyLists { get; }

    DbSet<VocabularyListItem> VocabularyListItems { get; }

    DbSet<ReviewCard> ReviewCards { get; }

    DbSet<ReviewLog> ReviewLogs { get; }

    DbSet<WordStudyContent> WordStudyContents { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
