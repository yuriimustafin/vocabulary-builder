using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public record UpsertWordCommand : IRequest<int>
{
    public string Headword { get; init; } = string.Empty;
    public string? Transcription { get; init; }
    public string? PartOfSpeech { get; init; }
    public int? Frequency { get; init; }
    public List<string>? Examples { get; init; }
    public List<Sense>? Senses { get; init; }
    
    // Properties for creating WordEncounter
    public WordEncounterSource Source { get; init; } = WordEncounterSource.Manual;
    public string? SourceIdentifier { get; init; }
    public string? Context { get; init; }
    public string? Notes { get; init; }
    
    // Dictionary sources for caching (optional)
    public List<WordDictionarySource>? DictionarySources { get; init; }
}

public class UpsertWordCommandHandler : IRequestHandler<UpsertWordCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public UpsertWordCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<int> Handle(UpsertWordCommand request, CancellationToken cancellationToken)
    {
        var existingWord = await _context.Words
            .Include(w => w.WordEncounters)
            .Include(w => w.Senses)
            .FirstOrDefaultAsync(w => w.Headword == request.Headword, cancellationToken);

        if (existingWord == null)
        {
            // Look up frequency if not provided
            var frequency = request.Frequency;
            if (!frequency.HasValue)
            {
                frequency = await _sender.Send(new GetWordFrequencyQuery(request.Headword), cancellationToken);
            }

            // Create new word
            var newWord = new Word
            {
                Headword = request.Headword,
                Transcription = request.Transcription,
                PartOfSpeech = request.PartOfSpeech,
                Frequency = frequency,
                Examples = request.Examples,
                Senses = request.Senses
            };

            _context.Words.Add(newWord);
            await _context.SaveChangesAsync(cancellationToken);
            
            // Add dictionary sources if provided
            if (request.DictionarySources != null && request.DictionarySources.Any())
            {
                foreach (var source in request.DictionarySources)
                {
                    source.WordId = newWord.Id;
                    _context.WordDictionarySources.Add(source);
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
            
            // Create the encounter record
            await CreateWordEncounter(newWord.Id, request, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            
            return newWord.Id;
        }
        else
        {
            // Update existing word (only if new information is provided)
            existingWord.Transcription = request.Transcription ?? existingWord.Transcription;
            existingWord.PartOfSpeech = request.PartOfSpeech ?? existingWord.PartOfSpeech;
            
            // Set frequency: use provided value, or look up if not provided and not already set
            if (request.Frequency.HasValue)
            {
                existingWord.Frequency = request.Frequency;
            }
            else if (!existingWord.Frequency.HasValue)
            {
                existingWord.Frequency = await _sender.Send(new GetWordFrequencyQuery(request.Headword), cancellationToken);
            }
            
            existingWord.Examples = request.Examples ?? existingWord.Examples;
            
            // Merge senses: add only new senses that don't already exist
            if (request.Senses != null && request.Senses.Any())
            {
                existingWord.Senses ??= new List<Sense>();
                
                foreach (var newSense in request.Senses)
                {
                    // Check if a sense with the same definition already exists
                    var isDuplicate = existingWord.Senses.Any(s => 
                        s.Definition.Equals(newSense.Definition, StringComparison.OrdinalIgnoreCase));
                    
                    if (!isDuplicate)
                    {
                        existingWord.Senses.Add(newSense);
                    }
                }
            }

            _context.Words.Update(existingWord);
            
            // Add new dictionary sources if provided (unique constraint will prevent duplicates)
            if (request.DictionarySources != null && request.DictionarySources.Any())
            {
                foreach (var source in request.DictionarySources)
                {
                    // Check if this source type already exists
                    var existingSource = await _context.WordDictionarySources
                        .FirstOrDefaultAsync(
                            wds => wds.WordId == existingWord.Id && wds.SourceType == source.SourceType,
                            cancellationToken);
                    
                    if (existingSource == null)
                    {
                        source.WordId = existingWord.Id;
                        _context.WordDictionarySources.Add(source);
                    }
                }
            }
            
            // Create new encounter record (idempotency check based on SourceIdentifier)
            await CreateWordEncounter(existingWord.Id, request, cancellationToken);
            
            await _context.SaveChangesAsync(cancellationToken);
            
            return existingWord.Id;
        }
    }

    private async Task CreateWordEncounter(int wordId, UpsertWordCommand request, CancellationToken cancellationToken)
    {
        // Generate SourceIdentifier from today's date if not provided (for manual entries)
        var sourceIdentifier = request.SourceIdentifier ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        
        // Check if this encounter already exists (idempotency check)
        var existingEncounter = await _context.WordEncounters
            .FirstOrDefaultAsync(we => 
                we.WordId == wordId && 
                we.SourceIdentifier == sourceIdentifier &&
                we.Source == request.Source, 
                cancellationToken);

        if (existingEncounter != null)
        {
            // Encounter already exists, don't create duplicate
            return;
        }

        var encounter = new WordEncounter
        {
            WordId = wordId,
            Source = request.Source,
            SourceIdentifier = sourceIdentifier,
            Context = request.Context,
            Notes = request.Notes
        };

        _context.WordEncounters.Add(encounter);
    }
}
