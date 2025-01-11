﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
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
            // TODO: Move note type and deck name to settings
            /* 
                #separator:Semicolon
                #html:true
                #deck column:1 
            */
            result.Append("English::Vocabulary;");
            result.Append(word.Headword + ";");
            result.Append($"\"{ClozeWholeString(word.Headword)} &nbsp;&nbsp;" +
                $"{(word.Transcription is null ? "" : ClozeWholeString(word.Transcription))}<br />");
            for (var i = 0; i < word.Senses.Count(); i++)
            {
                var sense = word.Senses.ElementAt(i);
                var senseDefinition = ClozeWholeString(
                    ClozeWord(
                        MakeValidLine(sense.Definition), 
                        word.Headword), 
                        2);
                result.Append($"( {i + 1} ) {senseDefinition}.");
                if (sense.Examples.Any())
                {
                    result.Append("<details><summary>Samples</summary><p>");
                    var example = String.Join(" | ", sense.Examples);
                    result.Append(ClozeWord(MakeValidLine(example), word.Headword));
                    result.Append("</p></details>");
                }
            }
            result.Append("\";");
            if (word.Headword.Length > 0)
            {
                result.Append(word.Headword[0] + ";");
            }
            result.Append("\r\n");
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
