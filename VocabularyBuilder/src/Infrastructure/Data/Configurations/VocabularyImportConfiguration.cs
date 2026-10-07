using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Entities.Imports;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

public class VocabularyImportConfiguration : IEntityTypeConfiguration<VocabularyImport>
{
    public void Configure(EntityTypeBuilder<VocabularyImport> builder)
    {
        builder.Property(i => i.Name).HasMaxLength(300);
        builder.Property(i => i.FileName).HasMaxLength(300);
        builder.Property(i => i.SourceIdentifierBase).HasMaxLength(300);
        builder.Property(i => i.Error).HasMaxLength(2000);

        builder.HasMany(i => i.Items)
            .WithOne(item => item.Import)
            .HasForeignKey(item => item.ImportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.OwnerId, i.StartedAtUtc });
    }
}

public class VocabularyImportItemConfiguration : IEntityTypeConfiguration<VocabularyImportItem>
{
    public void Configure(EntityTypeBuilder<VocabularyImportItem> builder)
    {
        builder.Property(i => i.Headword).HasMaxLength(200);
        builder.Property(i => i.SourceTerm).HasMaxLength(500);
        builder.Property(i => i.Reason).HasMaxLength(100);

        // The import brought the word in whether or not it is still here
        builder.HasOne(i => i.Word)
            .WithMany()
            .HasForeignKey(i => i.WordId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.WordId);
    }
}
