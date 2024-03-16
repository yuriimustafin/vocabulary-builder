using System.Reflection;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;
using System.Reflection.Emit;

namespace VocabularyBuilder.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<TodoList> TodoLists => Set<TodoList>();

    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    public DbSet<Word> Words => Set<Word>();

    public DbSet<ImportedBookWord> ImportedBookWords => Set<ImportedBookWord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<ImportedBookWord>()
            .HasOne(ibw => ibw.Word)
            .WithMany()
            .HasForeignKey(ibw => ibw.WordId);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }
}
