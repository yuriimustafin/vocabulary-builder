using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Infrastructure.Exporters;
internal class AnkiClozeCsvExporter : IWordsExporter
{
    private readonly AnkiExportOptions _options;

    public AnkiClozeCsvExporter(IOptions<AnkiExportOptions> options)
    {
        _options = options.Value;
    }

    public string ExportWords(IEnumerable<Word> words)
    {
        var result = new StringBuilder();
        foreach (var word in words)
        {
            if (word.Senses is null)
                continue;
            // TODO: Move note type to settings
            /* 
                #separator:Semicolon
                #html:true
                #deck column:1 
            */
            result.Append(_options.GetDeckName(word.Language) + ";");
            result.Append(word.Headword + ";");
            result.Append($"\"<span class='headword'>{ClozeWholeString(FormatHeadword(word))}</span> &nbsp;&nbsp;" +
                $"{
                    (word.Transcription is null 
                    ? "" 
                    : "<span class='transcription'>" + ClozeWholeString(word.Transcription) + "</span>")
                    }<br />");
            result.Append(word.PartOfSpeech is null ? "" : $"<span class='pos'>{word.PartOfSpeech}</span>");
            for (var i = 0; i < word.Senses.Count(); i++)
            {
                var sense = word.Senses.ElementAt(i);
                var senseDefinition = ClozeWholeString(
                    ClozeWord(
                        MakeValidLine(sense.Definition), 
                        word.Headword), 
                        2);
                result.Append($"<span class='def'>( {i + 1} ) {senseDefinition}.</span>");
                if (sense.Examples.Any())
                {
                    result.Append("<details class='samples'><summary>Samples</summary><p>");
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
            // Add SyncId as the last column for syncing with external systems
            result.Append(word.SyncId?.ToString() ?? "");
            result.Append("\r\n");
        }
        return result.ToString();
    }

    /// <summary>
    /// Colours used for articles, matching the app. Set inline because the note type's
    /// stylesheet lives in Anki, not here; the class is kept so it can still override them.
    /// </summary>
    private static readonly Dictionary<GrammaticalGender, string> ArticleColours = new()
    {
        [GrammaticalGender.Masculine] = "#1565c0",
        [GrammaticalGender.Feminine] = "#c62828",
        [GrammaticalGender.Common] = "#6a1b9a"
    };

    /// <summary>
    /// "la maison" with the article coloured by gender. The article sits inside the cloze
    /// with the word, so recalling the word means recalling its gender too.
    /// </summary>
    private static string FormatHeadword(Word word)
    {
        var article = word.GetArticle();
        if (article is null)
        {
            return word.GetHeadword();
        }

        var gender = article.Gender.ToString().ToLowerInvariant();
        var articleHtml =
            $"<span class='article article-{gender}' style='color:{ArticleColours[article.Gender]}'>{article.Definite}</span>";

        return article.IsElided
            ? articleHtml + word.Headword
            : $"{articleHtml} {word.Headword}";
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
