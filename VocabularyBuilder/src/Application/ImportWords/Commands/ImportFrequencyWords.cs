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
        // TODO: Finish and test the method
        var multilineFromFile = new List<string>();
        foreach(var line in multilineFromFile)
        {
            var lemmaAndForms = line.Split(" -> ");
        }
        var word = await _context.Words.FirstOrDefaultAsync();
        return 0;
    }

    private FrequencyWord SaveLemma(string lemma)
    {
        var lemmaAndFrequency = lemma.Split('/');
        var word = new FrequencyWord() { Headword = lemmaAndFrequency[0] };
        if (lemmaAndFrequency.Length > 1 && int.TryParse(lemmaAndFrequency[1], out var frequency))
        {
            word.Frequency = frequency;
        }
        return word;
    }


    private FrequencyWord SaveDerivedForms(FrequencyWord lemma, string allForms)
    {
        var forms = allForms.Split(",");
        lemma.DerivedForms = forms
            .Select(x => new FrequencyWord() { Headword = x })
            .ToList();
        return lemma;
    }

}
