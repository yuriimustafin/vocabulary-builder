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

    public DbSet<FrequencyWord> FrequencyWords => Set<FrequencyWord>();

    public DbSet<ImportedBookWord> ImportedBookWords => Set<ImportedBookWord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
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

        builder.Entity<ImportedBookWord>()
            .HasOne(ibw => ibw.Word)
            .WithMany()
            .HasForeignKey(ibw => ibw.WordId);

        builder.Entity<FrequencyWord>()
            .HasMany(fw => fw.DerivedForms)
            .WithOne(fw => fw.Lemma)
            .HasForeignKey(fw => fw.LemmaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }
}
