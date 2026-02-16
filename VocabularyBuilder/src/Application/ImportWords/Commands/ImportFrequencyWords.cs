using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.ImportWords.Commands;

public record ImportFrequencyWordsCommand(string FilePath) : IRequest<int>;
public class ImportFrequencyWords : IRequestHandler<ImportFrequencyWordsCommand, int>
{
    private readonly IApplicationDbContext _context;

    public ImportFrequencyWords(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(ImportFrequencyWordsCommand request, CancellationToken cancellationToken)
    {
        if (!File.Exists(request.FilePath))
        {
            throw new FileNotFoundException($"Frequency words file not found: {request.FilePath}");
        }

        var lines = await File.ReadAllLinesAsync(request.FilePath, cancellationToken);
        var importedCount = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(" -> ");
            if (parts.Length == 0)
                continue;

            // Parse and create the lemma (main word with frequency)
            var lemma = CreateLemma(parts[0]);
            _context.FrequencyWords.Add(lemma);
            
            // Save to get the ID for the lemma
            await _context.SaveChangesAsync(cancellationToken);
            
            // Parse and create derived forms if they exist
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var derivedForms = CreateDerivedForms(parts[1], lemma.Id);
                foreach (var form in derivedForms)
                {
                    _context.FrequencyWords.Add(form);
                }
            }

            importedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return importedCount;
    }

    private FrequencyWord CreateLemma(string lemmaText)
    {
        var parts = lemmaText.Split('/');
        var lemma = new FrequencyWord 
        { 
            Headword = parts[0].Trim(),
            Frequency = null,
            BaseFormId = null // Base forms have no parent
        };

        if (parts.Length > 1 && int.TryParse(parts[1], out var frequency))
        {
            lemma.Frequency = frequency;
        }

        return lemma;
    }

    private List<FrequencyWord> CreateDerivedForms(string allFormsText, int baseFormId)
    {
        var forms = allFormsText.Split(",");
        return forms
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new FrequencyWord 
            { 
                Headword = x.Trim(),
                Frequency = null, // Derived forms don't have frequency
                BaseFormId = baseFormId // Reference to the base form
            })
            .ToList();
    }

}
