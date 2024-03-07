using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Domain.Entities.ImportedBook;
using AngleSharp.Dom;
using AngleSharp;
using AngleSharp.Html.Parser;
using VocabularyBuilder.Domain.Entities;

namespace VocabularyBuilder.Infrastructure.Parsers;
public class BookImportParser : IBookImportParser
{
    public async Task<IList<ImportedBookWord>> GetWords(string htmlContent)
    {
        var result = new List<ImportedBookWord>();
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(htmlContent);

        var elements = document.QuerySelector(".bodyContainer")?.QuerySelectorAll("div");
        if (elements is null)
        {
            return result;
        }

        var currentSection = String.Empty;
        var currentNoteHeading = String.Empty;
        for (var i = 0; i < elements.Length; i++)
        {
            if (elements[i] is null)
            {
                continue;
            }
            if (elements[i].ClassName == "sectionHeading")
            {
                currentSection = elements[i]?.InnerHtml;
                continue;
            }
            if (elements[i].ClassName == "noteHeading")
            {
                currentNoteHeading = elements[i]?.InnerHtml;
                continue;
            }
            if (elements[i].ClassName == "noteText")
            {
                result.Add(
                    new ImportedBookWord() 
                    { 
                        Headword = elements[i].InnerHtml, 
                        Heading = currentNoteHeading,
                        Status = ImportWordStatus.Added
                    });
            }
        }
        return result;

    }
}
