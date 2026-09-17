using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

public class WordFormConfiguration : IEntityTypeConfiguration<WordForm>
{
    public void Configure(EntityTypeBuilder<WordForm> builder)
    {
        builder.Property(wf => wf.Form)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(wf => wf.Mood)
            .HasMaxLength(100);

        builder.Property(wf => wf.Tense)
            .HasMaxLength(100);

        builder.Property(wf => wf.Person)
            .HasMaxLength(100);
    }
}
