using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Entities.Study;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

public class ReviewLogConfiguration : IEntityTypeConfiguration<ReviewLog>
{
    public void Configure(EntityTypeBuilder<ReviewLog> builder)
    {
        // The answer outlives the card it was given on - see ReviewLog - and goes only with
        // the word itself
        builder.HasOne(rl => rl.ReviewCard)
            .WithMany()
            .HasForeignKey(rl => rl.ReviewCardId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(rl => rl.Word)
            .WithMany()
            .HasForeignKey(rl => rl.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(rl => rl.Answer).HasMaxLength(500);
        builder.Property(rl => rl.AnswerMatch).HasMaxLength(32);
        builder.Property(rl => rl.VoidReason).HasMaxLength(32);

        builder.HasIndex(rl => new { rl.WordId, rl.ReviewedAtUtc });

        // Idempotency: a repeated submit of the same rendered exercise scores once.
        builder.HasIndex(rl => rl.AttemptId)
            .IsUnique();

        builder.HasIndex(rl => new { rl.ReviewCardId, rl.ReviewedAtUtc });
    }
}
