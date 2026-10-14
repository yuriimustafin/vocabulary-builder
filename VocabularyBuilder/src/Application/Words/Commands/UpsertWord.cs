using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Entities.Imports;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;

public record UpsertWordCommand : IRequest<int>
{
    public string Headword { get; init; } = string.Empty;    public Language Language { get; init; } = Language.English;    public string? Transcription { get; init; }
    public string? PartOfSpeech { get; init; }

    /// <summary>Gender of the primary meaning, when it is a noun.</summary>
    public GrammaticalGender? Gender { get; init; }

    public bool IsPluralOnly { get; init; }
    public int? Frequency { get; init; }
    public List<string>? Examples { get; init; }
    public List<Sense>? Senses { get; init; }

    /// <summary>
    /// Labels to record the word under. Added to whatever the word already carries rather
    /// than replacing them.
    /// </summary>
    public List<string>? Tags { get; init; }
    
    // Properties for creating WordEncounter
    public WordEncounterSource Source { get; init; } = WordEncounterSource.Manual;
    public string? SourceIdentifier { get; init; }
    public string? Context { get; init; }
    public string? Notes { get; init; }

    /// <summary>
    /// The word as it was met, when that may differ from the headword - "prend" for
    /// "prendre". Recorded on the encounter, and a form no example sentence has yet is
    /// asked for when the word's study content is next filled in.
    /// </summary>
    public string? EncounterForm { get; init; }

    /// <summary>
    /// The sentence the word was met in, when the source keeps one. Kept as an example for
    /// the form it was met in - there is no better example than the one actually read.
    /// </summary>
    public string? EncounterSentence { get; init; }

    /// <summary>
    /// False when the upsert only fills the word in - a dictionary lookup is not a meeting
    /// with the word, and must not count as one.
    /// </summary>
    public bool RecordEncounter { get; init; } = true;

    /// <summary>
    /// The import this upsert is part of, if any. The word is filed under it - new or not,
    /// and whether or not the encounter was - so the import can show everything it touched.
    /// </summary>
    public int? ImportId { get; init; }

    /// <summary>
    /// The term as the import source wrote it, when that differs from the headword:
    /// "Vous allez" for "aller". Defaults to the headword.
    /// </summary>
    public string? ImportSourceTerm { get; init; }

    // Dictionary sources for caching (optional)
    public List<WordDictionarySource>? DictionarySources { get; init; }

    // Inflected forms, e.g. a verb's conjugation (optional)
    public List<WordForm>? Forms { get; init; }
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
            .FirstOrDefaultAsync(w => w.Headword == request.Headword && w.Language == request.Language, cancellationToken);

        if (existingWord == null)
        {
            // Look up frequency if not provided
            var frequency = request.Frequency;
            if (!frequency.HasValue)
            {
                frequency = await _sender.Send(new GetWordFrequencyQuery(request.Headword, request.Language), cancellationToken);
            }

            // Create new word
            var newWord = new Word
            {
                Headword = request.Headword,
                Language = request.Language,
                Transcription = request.Transcription,
                PartOfSpeech = request.PartOfSpeech,
                Gender = request.Gender,
                IsPluralOnly = request.IsPluralOnly,
                Frequency = frequency,
                Examples = request.Examples,
                Senses = request.Senses,
                Tags = request.Tags is { Count: > 0 } ? WordTags.Merge(null, request.Tags) : null
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
            
            await SaveWordForms(newWord.Id, request, cancellationToken);
            
            // Create the encounter record
            var encounter = await CreateWordEncounter(newWord, request, cancellationToken);
            await FileUnderImport(newWord, created: true, encounter, request, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            
            return newWord.Id;
        }
        else
        {
            // Update existing word (only if new information is provided)
            existingWord.Transcription = request.Transcription ?? existingWord.Transcription;
            existingWord.PartOfSpeech = request.PartOfSpeech ?? existingWord.PartOfSpeech;

            // Gender and number travel together: a request that knows the gender is
            // authoritative for both, one that does not leaves them as they were
            if (request.Gender.HasValue)
            {
                existingWord.Gender = request.Gender;
                existingWord.IsPluralOnly = request.IsPluralOnly;
            }
            
            // Set frequency: use provided value, or look up if not provided and not already set
            if (request.Frequency.HasValue)
            {
                existingWord.Frequency = request.Frequency;
            }
            else if (!existingWord.Frequency.HasValue)
            {
                existingWord.Frequency = await _sender.Send(new GetWordFrequencyQuery(request.Headword, request.Language), cancellationToken);
            }
            
            existingWord.Examples = request.Examples ?? existingWord.Examples;

            // Tags accumulate: meeting a word again under a new label must not lose the
            // label it was first collected under
            if (request.Tags is { Count: > 0 })
            {
                existingWord.Tags = WordTags.Merge(existingWord.Tags, request.Tags);
            }
            
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
            
            await SaveWordForms(existingWord.Id, request, cancellationToken);
            
            // Create new encounter record (idempotency check based on SourceIdentifier)
            var encounter = await CreateWordEncounter(existingWord, request, cancellationToken);
            await FileUnderImport(existingWord, created: false, encounter, request, cancellationToken);
            
            await _context.SaveChangesAsync(cancellationToken);
            
            return existingWord.Id;
        }
    }

    /// <summary>
    /// Record the word's inflected forms, adding only cells not already stored so that
    /// re-importing a word does not duplicate its conjugation. A cell is the form together
    /// with where it sits: "prends" is stored for both "je" and "tu".
    /// </summary>
    private async Task SaveWordForms(int wordId, UpsertWordCommand request, CancellationToken cancellationToken)
    {
        if (request.Forms == null || !request.Forms.Any())
        {
            return;
        }

        var existingForms = await _context.WordForms
            .Where(wf => wf.WordId == wordId)
            .Select(wf => new { wf.Mood, wf.Tense, wf.Person, wf.Form })
            .ToListAsync(cancellationToken);

        var known = existingForms
            .Select(wf => (wf.Mood, wf.Tense, wf.Person, wf.Form))
            .ToHashSet();

        foreach (var form in request.Forms)
        {
            if (string.IsNullOrWhiteSpace(form.Form) || !known.Add((form.Mood, form.Tense, form.Person, form.Form)))
            {
                continue;
            }

            form.WordId = wordId;
            form.Language = request.Language;
            _context.WordForms.Add(form);
        }
    }

    /// <summary>
    /// Records that the import brought this term in, and onto which word.
    /// </summary>
    /// <remarks>
    /// A word an earlier term of the same import created counts as created for this term too.
    /// Otherwise "une randonnée" would show as new and "la randonnée", two lines further down
    /// the same file, as already known - known only since a moment ago, and only because of
    /// this very import.
    /// </remarks>
    private async Task FileUnderImport(
        Word word, bool created, EncounterOutcome encounter, UpsertWordCommand request, CancellationToken cancellationToken)
    {
        if (request.ImportId is not > 0)
        {
            return;
        }

        var importId = request.ImportId.Value;

        var createdHere = created || await _context.VocabularyImportItems.AnyAsync(
            i => i.ImportId == importId && i.WordId == word.Id && i.Outcome == ImportItemOutcome.Created,
            cancellationToken);

        _context.VocabularyImportItems.Add(new VocabularyImportItem
        {
            ImportId = importId,
            WordId = word.Id,
            Headword = word.Headword,
            SourceTerm = request.ImportSourceTerm ?? request.Headword,
            Outcome = createdHere ? ImportItemOutcome.Created : ImportItemOutcome.Existing,
            EncounterAdded = encounter.Added,
            Form = encounter.Form,
            ExampleAdded = encounter.Coverage == FormCoverage.ExampleAdded,
            ContentReopened = encounter.Coverage == FormCoverage.ContentReopened
        });
    }

    /// <summary>What recording an encounter did, for the import to file.</summary>
    /// <param name="Added">False when it was already recorded, or not asked for.</param>
    /// <param name="Form">The form it was met in, if one was given.</param>
    private sealed record EncounterOutcome(bool Added, string? Form = null, FormCoverage Coverage = FormCoverage.None);

    private enum FormCoverage
    {
        None,

        /// <summary>The sentence the form was met in became an example of it.</summary>
        ExampleAdded,

        /// <summary>No example used the form, so the word's finished content was opened again.</summary>
        ContentReopened
    }

    private async Task<EncounterOutcome> CreateWordEncounter(Word word, UpsertWordCommand request, CancellationToken cancellationToken)
    {
        var wordId = word.Id;

        if (!request.RecordEncounter)
        {
            return new EncounterOutcome(false);
        }

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
            return new EncounterOutcome(false);
        }

        var form = NormaliseForm(request.EncounterForm);

        var encounter = new WordEncounter
        {
            WordId = wordId,
            Source = request.Source,
            SourceIdentifier = sourceIdentifier,
            Context = request.Context,
            Notes = request.Notes,
            Form = form
        };

        _context.WordEncounters.Add(encounter);

        var coverage = form is null
            ? FormCoverage.None
            : await CoverForm(word, form, request, cancellationToken);

        return new EncounterOutcome(true, form, coverage);
    }

    /// <summary>
    /// Makes sure the form a word was met in will have an example to be practised on: the
    /// sentence it was met in when that contains it, and otherwise a request for one the
    /// next time the word's study content is filled in.
    /// </summary>
    private async Task<FormCoverage> CoverForm(Word word, string form, UpsertWordCommand request, CancellationToken cancellationToken)
    {
        var wordId = word.Id;
        var sentence = request.EncounterSentence;

        var examples = await _context.StudyExamples
            .Where(e => e.WordId == wordId)
            .ToListAsync(cancellationToken);

        var trimmed = sentence?.Trim();

        if (!string.IsNullOrEmpty(trimmed)
            && HeadwordText.Contains(trimmed, form)
            && !examples.Any(e => string.Equals(e.Sentence, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            _context.StudyExamples.Add(new StudyExample { WordId = wordId, Sentence = trimmed, Form = form });
            return FormCoverage.ExampleAdded;
        }

        if (examples.Any(e => string.Equals(e.Form, form, StringComparison.OrdinalIgnoreCase)))
        {
            return FormCoverage.None;
        }

        // Content already filled in is opened again, so the missing form is asked for. A
        // word enrichment gave up on stays given up; a word never filled in will be, form
        // included, the first time it is studied.
        var content = await _context.WordStudyContents
            .FirstOrDefaultAsync(c => c.WordId == wordId, cancellationToken);

        if (content is not { Status: StudyContentStatus.Ready })
        {
            return FormCoverage.None;
        }

        content.Status = StudyContentStatus.Pending;
        content.ClaimedAtUtc = null;

        // The model call this costs happens later, in a study session - remembering the import
        // here is what lets that call be filed under it
        content.ReopenedByImportId = request.ImportId is > 0 ? request.ImportId : null;

        _context.RecordActivity(
            ActivityAction.StudyContentReopened,
            word,
            summary: $"Met as \"{form}\", which no example uses - study content will be generated again",
            details: new { form, request.Source, request.SourceIdentifier },
            importId: content.ReopenedByImportId);

        return FormCoverage.ContentReopened;
    }

    private static string? NormaliseForm(string? form)
    {
        var trimmed = form?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
