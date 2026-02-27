using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Samples.Entities;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.ImportWords.Commands;

public record ImportFrequencyWordsCommand(string FilePath, Language Language = Language.English) : IRequest<int>;
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
        
        // Disable auto detect changes for better performance if supported
        var supportsChangeTracker = _context.ChangeTracker != null;
        if (supportsChangeTracker)
        {
            _context.ChangeTracker!.AutoDetectChangesEnabled = false;
        }
        
        var batchSize = 1000;
        var importedCount = 0;
        var currentBatch = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(" -> ");
            if (parts.Length == 0)
                continue;

            // Parse and create the lemma (main word with frequency)
            var lemma = CreateLemma(parts[0], request.Language);
            _context.FrequencyWords.Add(lemma);
            
            // Parse and create derived forms if they exist
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var derivedForms = CreateDerivedFormsWithParent(parts[1], lemma, request.Language);
                foreach (var form in derivedForms)
                {
                    _context.FrequencyWords.Add(form);
                }
            }

            currentBatch++;
            importedCount++;

            // Save in batches to avoid memory issues
            if (currentBatch >= batchSize)
            {
                await _context.SaveChangesAsync(cancellationToken);
                if (supportsChangeTracker)
                {
                    _context.ChangeTracker!.Clear(); // Clear tracking to free memory
                }
                currentBatch = 0;
            }
        }

        // Save remaining items
        if (currentBatch > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        
        // Re-enable auto detect changes if it was disabled
        if (supportsChangeTracker)
        {
            _context.ChangeTracker!.AutoDetectChangesEnabled = true;
        }

        return importedCount;
    }

    private FrequencyWord CreateLemma(string lemmaText, Language language)
    {
        var parts = lemmaText.Split('/');
        var lemma = new FrequencyWord 
        { 
            Headword = parts[0].Trim(),
            Language = language,
            Frequency = null,
            BaseFormId = null // Base forms have no parent
        };

        if (parts.Length > 1 && int.TryParse(parts[1], out var frequency))
        {
            lemma.Frequency = frequency;
        }

        return lemma;
    }

    private List<FrequencyWord> CreateDerivedFormsWithParent(string allFormsText, FrequencyWord baseForm, Language language)
    {
        var forms = allFormsText.Split(",");
        return forms
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new FrequencyWord 
            { 
                Headword = x.Trim(),
                Language = language,
                Frequency = null, // Derived forms don't have frequency
                BaseForm = baseForm // Set navigation property, not ID
            })
            .ToList();
    }

}
