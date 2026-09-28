using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;

public class SenseConfiguration : IEntityTypeConfiguration<Sense>
{
    public void Configure(EntityTypeBuilder<Sense> builder)
    {
        // Named explicitly because the table predates the DbSet. EF names a table after the
        // property that exposes it, so adding "Senses" to the context would otherwise rename
        // an existing table for no reason other than that it had been given a name.
        builder.ToTable("Sense");
    }
}
