using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Entities.Study;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

public class ReviewCardConfiguration : IEntityTypeConfiguration<ReviewCard>
{
    public void Configure(EntityTypeBuilder<ReviewCard> builder)
    {
        builder.HasOne(rc => rc.Word)
            .WithMany()
            .HasForeignKey(rc => rc.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ladder style: exactly one card per word.
        builder.HasIndex(rc => rc.WordId)
            .IsUnique();

        // The due-card query runs on every page load and filters on both columns.
        builder.HasIndex(rc => new { rc.State, rc.DueAtUtc });

        // Backs the daily new-card cap.
        builder.HasIndex(rc => rc.IntroducedAtUtc);
    }
}
