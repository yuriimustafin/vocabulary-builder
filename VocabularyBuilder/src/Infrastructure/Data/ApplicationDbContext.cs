using System.Reflection;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;
using VocabularyBuilder.Domain.Entities.Frequency;
using System.Reflection.Emit;

namespace VocabularyBuilder.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<TodoList> TodoLists => Set<TodoList>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    public DbSet<Word> Words => Set<Word>();

    public DbSet<WordEncounter> WordEncounters => Set<WordEncounter>();
    
    public DbSet<WordDictionarySource> WordDictionarySources => Set<WordDictionarySource>();

    public DbSet<FrequencyWord> FrequencyWords => Set<FrequencyWord>();

    public DbSet<ImportedBookWord> ImportedBookWords => Set<ImportedBookWord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Create unique index for Word: Headword + Language must be unique
        builder.Entity<Word>()
            .HasIndex(w => new { w.Headword, w.Language })
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
            .HasIndex(ibw => new { ibw.Headword, ibw.Language });

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }
}
