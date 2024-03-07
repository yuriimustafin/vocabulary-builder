using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Data.Configurations;
public class WordConfiguration : IEntityTypeConfiguration<Word>
{
    public void Configure(EntityTypeBuilder<Word> builder)
    {
        builder.Property(t => t.Headword)
            .HasMaxLength(200)
            .IsRequired();
    }
}
