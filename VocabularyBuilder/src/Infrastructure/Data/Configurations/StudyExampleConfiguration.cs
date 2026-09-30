using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Entities.Study;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

public class StudyExampleConfiguration : IEntityTypeConfiguration<StudyExample>
{
    public void Configure(EntityTypeBuilder<StudyExample> builder)
    {
        builder.HasOne(e => e.Word)
            .WithMany()
            .HasForeignKey(e => e.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Loaded per word, for every word in a session.
        builder.HasIndex(e => e.WordId);

        builder.Property(e => e.Sentence).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Translation).HasMaxLength(500);
        builder.Property(e => e.Form).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Collocation).HasMaxLength(100);
    }
}
