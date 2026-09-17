using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Entities.Study;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

public class WordStudyContentConfiguration : IEntityTypeConfiguration<WordStudyContent>
{
    public void Configure(EntityTypeBuilder<WordStudyContent> builder)
    {
        builder.HasOne(wsc => wsc.Word)
            .WithMany()
            .HasForeignKey(wsc => wsc.WordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotency: one content row per word, so concurrent enrichment requests collide
        // on the index rather than duplicating GPT calls.
        builder.HasIndex(wsc => wsc.WordId)
            .IsUnique();

        // Backs the startup sweep for claims abandoned by a crash.
        builder.HasIndex(wsc => new { wsc.Status, wsc.ClaimedAtUtc });

        builder.Property(wsc => wsc.PromptVersion)
            .HasMaxLength(20)
            .IsRequired();
    }
}
