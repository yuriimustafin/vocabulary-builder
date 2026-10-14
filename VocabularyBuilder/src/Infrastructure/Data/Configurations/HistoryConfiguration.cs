using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Entities.History;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

/// <remarks>
/// Enums here are stored by name. These tables are read in SQL as often as through the app,
/// and a number that meant WordDeleted last year must not mean something else after a member
/// is inserted above it.
///
/// No foreign keys to words or imports, so that neither being deleted takes history with it.
/// Pages are read newest first by id, which is monotonic for an append-only table and - unlike
/// a date - orderable in SQLite without loading everything.
/// </remarks>
public class ActivityLogEntryConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
{
    public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
    {
        builder.ToTable("ActivityLog");

        builder.Property(e => e.Action)
            .HasConversion<string>()
            .HasMaxLength(64);

        builder.Property(e => e.Headword).HasMaxLength(200);
        builder.Property(e => e.Summary).HasMaxLength(500);

        builder.HasIndex(e => new { e.OwnerId, e.Id });
        builder.HasIndex(e => e.WordId);
        builder.HasIndex(e => e.ImportId);
    }
}

public class ExternalCallLogConfiguration : IEntityTypeConfiguration<ExternalCallLog>
{
    public void Configure(EntityTypeBuilder<ExternalCallLog> builder)
    {
        builder.ToTable("ExternalCallLog");

        builder.Property(e => e.Provider)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(e => e.Purpose)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(e => e.Model).HasMaxLength(64);
        builder.Property(e => e.PromptVersion).HasMaxLength(20);
        builder.Property(e => e.Target).HasMaxLength(200);
        builder.Property(e => e.Url).HasMaxLength(1000);
        builder.Property(e => e.Error).HasMaxLength(2000);

        builder.HasIndex(e => new { e.OwnerId, e.Id });
        builder.HasIndex(e => e.WordId);
        builder.HasIndex(e => e.ImportId);
    }
}
