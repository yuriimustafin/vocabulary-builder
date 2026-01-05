using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public record UpsertWordCommand : IRequest<int>
{
    public string Headword { get; init; } = string.Empty;
    public string? Transcription { get; init; }
    public string? PartOfSpeech { get; init; }
    public int? Frequency { get; init; }
    public int EncounterCount { get; init; }
    public List<string>? Examples { get; init; }
}

public class UpsertWordCommandHandler : IRequestHandler<UpsertWordCommand, int>
{
    private readonly IApplicationDbContext _context;

    public UpsertWordCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(UpsertWordCommand request, CancellationToken cancellationToken)
    {
        var existingWord = await _context.Words
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Headword == request.Headword, cancellationToken);

        if (existingWord == null)
        {
            // Create new word
            var newWord = new Word
            {
                Headword = request.Headword,
                Transcription = request.Transcription,
                PartOfSpeech = request.PartOfSpeech,
                Frequency = request.Frequency,
                EncounterCount = request.EncounterCount,
                Examples = request.Examples
            };

            _context.Words.Add(newWord);
            await _context.SaveChangesAsync(cancellationToken);
            
            return newWord.Id;
        }
        else
        {
            // Update existing word
            existingWord.Transcription = request.Transcription ?? existingWord.Transcription;
            existingWord.PartOfSpeech = request.PartOfSpeech ?? existingWord.PartOfSpeech;
            existingWord.Frequency = request.Frequency ?? existingWord.Frequency;
            existingWord.EncounterCount++;
            existingWord.Examples = request.Examples ?? existingWord.Examples;

            _context.Words.Update(existingWord);
            await _context.SaveChangesAsync(cancellationToken);
            
            return existingWord.Id;
        }
    }
}
