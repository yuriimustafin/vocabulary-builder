using Microsoft.EntityFrameworkCore.ChangeTracking;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Entities.Imports;
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

    DbSet<VocabularyImport> VocabularyImports { get; }

    DbSet<VocabularyImportItem> VocabularyImportItems { get; }

    /// <summary>
    /// What each user did. Written through <c>RecordActivity</c>, alongside the change it
    /// describes, so that the entry is saved exactly when the change is.
    /// </summary>
    DbSet<ActivityLogEntry> ActivityLog { get; }

    /// <summary>
    /// Every request to a model or a dictionary site. Written by the recorder on a context of
    /// its own rather than through this one - see <c>IExternalCallRecorder</c>.
    /// </summary>
    DbSet<ExternalCallLog> ExternalCallLog { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
