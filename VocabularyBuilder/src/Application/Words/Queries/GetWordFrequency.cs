using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Words.Queries;

public record GetWordFrequencyQuery(string Headword, Language Language = Language.English) : IRequest<int?>;

public class GetWordFrequencyQueryHandler : IRequestHandler<GetWordFrequencyQuery, int?>
{
    private readonly IApplicationDbContext _context;

    public GetWordFrequencyQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int?> Handle(GetWordFrequencyQuery request, CancellationToken cancellationToken)
    {
        var headword = request.Headword.Trim().ToLowerInvariant();

        // Find all words with this headword and language
        var frequencyWords = await _context.FrequencyWords
            .Where(fw => fw.Headword.ToLower() == headword && fw.Language == request.Language)
            .Include(fw => fw.BaseForm)
            .ToListAsync(cancellationToken);

        if (!frequencyWords.Any())
            return null;

        // Get all possible frequencies: the word's own frequency or its base form's frequency
        var frequencies = frequencyWords
            .Select(fw => fw.Frequency ?? fw.BaseForm?.Frequency)
            .Where(f => f.HasValue)
            .Select(f => f!.Value);

        // Return the highest frequency
        return frequencies.Any() ? frequencies.Max() : null;
    }
}
