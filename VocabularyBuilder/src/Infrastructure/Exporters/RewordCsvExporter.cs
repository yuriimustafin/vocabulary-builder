using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Entities;

namespace VocabularyBuilder.Infrastructure.Exporters;
public class RewordCsvExporter : IWordsExporter
{
    public string ExportWords(IEnumerable<Word> words)
    {
        var result = new StringBuilder();
        foreach (var word in words)
        {
            result.Append($"\"{word.Headword}\";\"\";");
            result.Append("\"");
            var examples = new List<string>();
            for (var i = 0; i < word.Senses.Count(); i++)
            {
                var sense = word.Senses.ElementAt(i);
                var senseDefinition = MakeValidLine(sense.Definition);
                result.Append($"( {i + 1} ) {senseDefinition}. ");
                if (sense.Examples.Any())
                {
                    var example = String.Join(" | ", sense.Examples);
                    examples.Add(MakeValidLine(example));
                }
            }
            result.Append("\"");
            foreach (var example in examples)
            {
                result.Append($";\"{example}\";\"\"");
            }
            result.Append("\r\n");
        }
        return result.ToString();
    }

    private string MakeValidLine(string str)
    {
        string singleLine = str.Replace("\r\n", "").Replace("\n", "");
        return singleLine.Replace("\"", "");
    }
}
