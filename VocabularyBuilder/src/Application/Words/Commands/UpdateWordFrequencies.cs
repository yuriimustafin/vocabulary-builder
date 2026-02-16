using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Words.Queries;

namespace VocabularyBuilder.Application.Words.Commands;

public record UpdateWordFrequenciesCommand : IRequest<UpdateWordFrequenciesResult>;

public class UpdateWordFrequenciesResult
{
    public int TotalWords { get; set; }
    public int UpdatedWords { get; set; }
    public int NotFoundWords { get; set; }
}

public class UpdateWordFrequenciesCommandHandler : IRequestHandler<UpdateWordFrequenciesCommand, UpdateWordFrequenciesResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public UpdateWordFrequenciesCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<UpdateWordFrequenciesResult> Handle(UpdateWordFrequenciesCommand request, CancellationToken cancellationToken)
    {
        var result = new UpdateWordFrequenciesResult();

        // Get all words without frequency
        var wordsWithoutFrequency = await _context.Words
            .Where(w => w.Frequency == null)
            .ToListAsync(cancellationToken);

        result.TotalWords = wordsWithoutFrequency.Count;

        foreach (var word in wordsWithoutFrequency)
        {
            var frequency = await _sender.Send(new GetWordFrequencyQuery(word.Headword), cancellationToken);
            
            if (frequency.HasValue)
            {
                word.Frequency = frequency.Value;
                result.UpdatedWords++;
            }
            else
            {
                result.NotFoundWords++;
            }
        }

        if (result.UpdatedWords > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
