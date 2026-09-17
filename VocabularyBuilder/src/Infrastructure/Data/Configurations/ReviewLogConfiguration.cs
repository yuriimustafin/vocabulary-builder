using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Entities.Study;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

public class ReviewLogConfiguration : IEntityTypeConfiguration<ReviewLog>
{
    public void Configure(EntityTypeBuilder<ReviewLog> builder)
    {
        builder.HasOne(rl => rl.ReviewCard)
            .WithMany()
            .HasForeignKey(rl => rl.ReviewCardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotency: a repeated submit of the same rendered exercise scores once.
        builder.HasIndex(rl => rl.AttemptId)
            .IsUnique();

        builder.HasIndex(rl => new { rl.ReviewCardId, rl.ReviewedAtUtc });
    }
}
