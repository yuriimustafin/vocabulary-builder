using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Common.Interfaces;
public interface IWordsExporter
{
    string ExportWords(IEnumerable<Word> words);
}
