using System.Reflection;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Common;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Entities.Study;

namespace VocabularyBuilder.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly IUser? _user;

    /// <param name="user">
    /// Whose data this context sees and writes. Left out, the context belongs to nobody: every
    /// owned query comes back empty and saving a new owned entity throws.
    /// </param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUser? user = null)
        : base(options)
    {
        _user = user;
    }

    /// <summary>
    /// The user every owned query is scoped to. The query filters read it on each execution
    /// rather than when the model is built, so one model serves every user.
    /// </summary>
    private string? CurrentUserId => _user?.Id;

    public DbSet<TodoList> TodoLists => Set<TodoList>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    public DbSet<Word> Words => Set<Word>();

    public DbSet<WordEncounter> WordEncounters => Set<WordEncounter>();
    
    public DbSet<WordDictionarySource> WordDictionarySources => Set<WordDictionarySource>();

    public DbSet<WordForm> WordForms => Set<WordForm>();

    public DbSet<Sense> Senses => Set<Sense>();

    public DbSet<FrequencyWord> FrequencyWords => Set<FrequencyWord>();

    public DbSet<ImportedBookWord> ImportedBookWords => Set<ImportedBookWord>();

    public DbSet<VocabularyList> VocabularyLists => Set<VocabularyList>();

    public DbSet<VocabularyListItem> VocabularyListItems => Set<VocabularyListItem>();

    public DbSet<ReviewCard> ReviewCards => Set<ReviewCard>();

    public DbSet<ReviewLog> ReviewLogs => Set<ReviewLog>();

    public DbSet<WordStudyContent> WordStudyContents => Set<WordStudyContent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // A headword is unique within one user's vocabulary, not across users: each user
        // keeps their own copy of a word, and two users can both be learning "maison"
        builder.Entity<Word>()
            .HasIndex(w => new { w.OwnerId, w.Headword, w.Language })
            .IsUnique();
        
        builder.Entity<WordEncounter>()
            .HasOne(we => we.Word)
            .WithMany(w => w.WordEncounters)
            .HasForeignKey(we => we.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Create unique index for idempotency check
        builder.Entity<WordEncounter>()
            .HasIndex(we => new { we.WordId, we.Source, we.SourceIdentifier })
            .IsUnique()
            .HasFilter("[SourceIdentifier] IS NOT NULL");

        builder.Entity<WordDictionarySource>()
            .HasOne(wds => wds.Word)
            .WithMany(w => w.DictionarySources)
            .HasForeignKey(wds => wds.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Create unique index to prevent duplicate sources for same word
        builder.Entity<WordDictionarySource>()
            .HasIndex(wds => new { wds.WordId, wds.SourceType })
            .IsUnique();

        builder.Entity<WordForm>()
            .HasOne(wf => wf.Word)
            .WithMany()
            .HasForeignKey(wf => wf.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Looked up by written form when resolving an encounter to its lemma.
        // Deliberately not unique: one French form regularly belongs to several
        // words ("suis" to both etre and suivre).
        builder.Entity<WordForm>()
            .HasIndex(wf => new { wf.Form, wf.Language });

        builder.Entity<ImportedBookWord>()
            .HasOne(ibw => ibw.Word)
            .WithMany()
            .HasForeignKey(ibw => ibw.WordId);

        builder.Entity<FrequencyWord>()
            .HasMany(fw => fw.DerivedForms)
            .WithOne(fw => fw.BaseForm)
            .HasForeignKey(fw => fw.BaseFormId)
            .OnDelete(DeleteBehavior.Restrict);

        // Create unique index for FrequencyWord: Headword + Language must be unique
        builder.Entity<FrequencyWord>()
            .HasIndex(fw => new { fw.Headword, fw.Language })
            .IsUnique();
        
        // Create index for ImportedBookWord: Headword + Language for efficient lookups
        builder.Entity<ImportedBookWord>()
            .HasIndex(ibw => new { ibw.OwnerId, ibw.Headword, ibw.Language });

        // Configure VocabularyList relationship with cascade delete
        builder.Entity<VocabularyListItem>()
            .HasOne(vli => vli.List)
            .WithMany(vl => vl.Items)
            .HasForeignKey(vli => vli.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        ConfigureOwnership(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }

    /// <summary>
    /// Scopes every user's data to that user, on the way in and on the way out.
    /// </summary>
    /// <remarks>
    /// The filters are the guarantee, not the handlers. A handler that looks a word up by id
    /// finds nothing when the word is someone else's - it answers "not found" exactly as it
    /// would for an id that was never used, and has no code of its own to get wrong.
    ///
    /// Dependents are filtered too, through their parent, because several of them are queried
    /// directly: the study queue starts from review cards, and encounter counting looks words
    /// up by their inflected forms. A filter on the roots alone would leave those reading
    /// every user's rows.
    ///
    /// Frequency data is deliberately left out. It is reference data about the language,
    /// imported once and shared by everyone.
    /// </remarks>
    private void ConfigureOwnership(ModelBuilder builder)
    {
        ConfigureOwnedRoot<Word>(builder);
        ConfigureOwnedRoot<VocabularyList>(builder);
        ConfigureOwnedRoot<ImportedBookWord>(builder);
        ConfigureOwnedRoot<TodoList>(builder);

        builder.Entity<WordEncounter>().HasQueryFilter(e => e.Word.OwnerId == CurrentUserId);
        builder.Entity<WordDictionarySource>().HasQueryFilter(s => s.Word.OwnerId == CurrentUserId);
        builder.Entity<WordForm>().HasQueryFilter(f => f.Word.OwnerId == CurrentUserId);
        builder.Entity<WordStudyContent>().HasQueryFilter(c => c.Word.OwnerId == CurrentUserId);
        builder.Entity<ReviewCard>().HasQueryFilter(c => c.Word.OwnerId == CurrentUserId);
        builder.Entity<ReviewLog>().HasQueryFilter(l => l.ReviewCard.Word.OwnerId == CurrentUserId);
        builder.Entity<VocabularyListItem>().HasQueryFilter(i => i.List.OwnerId == CurrentUserId);
        builder.Entity<TodoItem>().HasQueryFilter(i => i.List.OwnerId == CurrentUserId);

        // A sense has no navigation back to its word, only the key, so it is matched against
        // the words this user can see - which carries the word's own filter along with it
        builder.Entity<Sense>().HasQueryFilter(s =>
            Words.Any(w => w.Id == EF.Property<int?>(s, "WordId")));
    }

    private void ConfigureOwnedRoot<TEntity>(ModelBuilder builder) where TEntity : class, IOwnedEntity
    {
        var entity = builder.Entity<TEntity>();

        entity.Property(e => e.OwnerId).IsRequired();

        // Removing a user removes everything they collected
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasQueryFilter(e => e.OwnerId == CurrentUserId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignOwners();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AssignOwners();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Gives each new owned entity to the current user.
    /// </summary>
    /// <remarks>
    /// Done here rather than in an interceptor so that it holds for every context, including
    /// the ones tests build by hand without any interceptors registered. An owner that is
    /// already set is kept, which is what lets the administrator bootstrap hand rows to a
    /// particular user; nothing that takes input from a request sets one.
    /// </remarks>
    private void AssignOwners()
    {
        foreach (var entry in ChangeTracker.Entries<IOwnedEntity>())
        {
            if (entry.State != EntityState.Added || !string.IsNullOrEmpty(entry.Entity.OwnerId))
            {
                continue;
            }

            entry.Entity.OwnerId = CurrentUserId
                ?? throw new InvalidOperationException(
                    $"Cannot save a new {entry.Metadata.ClrType.Name} without a signed-in user to own it.");
        }
    }
}
