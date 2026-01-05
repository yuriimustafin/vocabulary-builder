using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Samples.Entities.ImportedBook;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Common.Interfaces;
public interface IAnkiDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
