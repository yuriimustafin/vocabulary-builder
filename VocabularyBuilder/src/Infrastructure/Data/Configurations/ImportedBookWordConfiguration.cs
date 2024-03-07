using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;
public class ImportedBookWordConfiguration : IEntityTypeConfiguration<ImportedBookWord>
{
    public void Configure(EntityTypeBuilder<ImportedBookWord> builder)
    {
        builder.Property(t => t.Headword)
            .HasMaxLength(200)
            .IsRequired();
    }
}
