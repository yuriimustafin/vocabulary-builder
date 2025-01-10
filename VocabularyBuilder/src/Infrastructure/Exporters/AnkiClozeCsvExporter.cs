using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Exporters;
internal class AnkiClozeCsvExporter : IWordsExporter
{
    public string ExportWords(IEnumerable<Word> words)
    {
        var result = new StringBuilder();
        foreach (var word in words)
        {
            if (word.Senses is null)
                continue;
            result.Append("Cloze;");
            result.Append($"\"{ClozeWholeString(word.Headword)}\r\n");
            for (var i = 0; i < word.Senses.Count(); i++)
            {
                var sense = word.Senses.ElementAt(i);
                var senseDefinition = ClozeWholeString(
                    ClozeWord(
                        MakeValidLine(sense.Definition), 
                        word.Headword), 
                    2);
                result.Append($"( {i + 1} ) {senseDefinition}. \r\n");
                if (sense.Examples.Any())
                {
                    result.Append("<details>\n<summary>Samples</summary>\n<p>");
                    var example = String.Join(" | ", sense.Examples);
                    result.Append(ClozeWord(MakeValidLine(example), word.Headword));
                    result.Append("</p>\n</details>");
                }
                result.Append("\r\n");
            }
            result.Append("\";\r\n");
        }
        return result.ToString();
    }

    private string MakeValidLine(string str)
    {
        return str.Replace("\"", "'");
    }

    private string ClozeWord(string str, string word, int clozeNumber = 1)
    {
        return str.Replace(word, $"{{{{c{clozeNumber}::{word}}}}}");
    }

    private string ClozeWholeString(string str, int clozeNumber = 1)
    {
        return $"{{{{c{clozeNumber}::{str}}}}}";
    }
}
