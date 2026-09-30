using System.Text.Json;
using System.Text.Json.Serialization;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Words.Commands;
using VocabularyBuilder.Domain.Entities.Study;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Study.Enrichment;

public enum EnrichmentOutcome
{
    /// <summary>The word has everything it needs already; nothing was generated.</summary>
    NothingMissing,

    /// <summary>A previous run already filled this word in.</summary>
    AlreadyFilled,

    /// <summary>Another run holds a fresh claim on this word.</summary>
    ClaimedElsewhere,

    Generated,

    /// <summary>Generation failed, or produced nothing usable, and will be retried.</summary>
    Failed,

    /// <summary>Generation has failed too many times; the word is left alone.</summary>
    GivenUp,

    WordNotFound
}

/// <summary>
/// Fills in whatever a word is missing before it can be studied.
/// </summary>
public record EnrichWordStudyContentCommand(int WordId) : IRequest<EnrichmentOutcome>;

/// <summary>
/// Generates only what is actually absent.
///
/// Every word reaches the model once per <see cref="StudyContentPrompt.Version"/>, for its
/// examples and connections. Gaps are computed against the dictionary data the app already
/// holds, so that call asks for a definition only when the dictionary had none, and a word
/// asked again - for a form met since - is asked only for what it is missing.
///
/// Every step is safe to repeat. The unique index on WordId keeps one row per word, a
/// fresh claim makes a concurrent run stand down, and a claim left behind by a crash goes
/// stale and is picked up again.
/// </summary>
public class EnrichWordStudyContentCommandHandler : IRequestHandler<EnrichWordStudyContentCommand, EnrichmentOutcome>
{
    private readonly IApplicationDbContext _context;
    private readonly IGptClient _gptClient;
    private readonly IStudyMaterialResolver _resolver;
    private readonly StudyOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ISender _sender;

    public EnrichWordStudyContentCommandHandler(
        IApplicationDbContext context,
        IGptClient gptClient,
        IStudyMaterialResolver resolver,
        StudyOptions options,
        TimeProvider timeProvider,
        ISender sender)
    {
        _context = context;
        _gptClient = gptClient;
        _resolver = resolver;
        _options = options;
        _timeProvider = timeProvider;
        _sender = sender;
    }

    /// <summary>
    /// Fills the word from the dictionary if it has not been, and re-reads it so the gaps are
    /// counted against what it now knows.
    /// </summary>
    /// <remarks>
    /// A dictionary that is unreachable or has no entry must not stop a word being studied -
    /// it simply goes on without an article, which is what happened before this step existed.
    /// </remarks>
    private async Task<Word> FillFromDictionary(Word word, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _sender.Send(new FillWordFromDictionaryCommand(word.Id), cancellationToken);

            if (outcome != DictionaryFillOutcome.Filled)
            {
                return word;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not fill '{word.Headword}' from the dictionary: {ex.Message}");
            return word;
        }

        return await _context.Words
            .Include(w => w.Senses)
            .FirstOrDefaultAsync(w => w.Id == word.Id, cancellationToken) ?? word;
    }

    public async Task<EnrichmentOutcome> Handle(EnrichWordStudyContentCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var word = await _context.Words
            .Include(w => w.Senses)
            .FirstOrDefaultAsync(w => w.Id == request.WordId, cancellationToken);

        if (word is null)
        {
            return EnrichmentOutcome.WordNotFound;
        }

        // Ask the dictionary first, then count what is still missing. A word imported as a
        // bare headword has no gender until something looks it up, and with no gender a
        // French noun is studied without its article. Filling it here rather than leaving it
        // to export also narrows the gaps, so the model is asked for less - sometimes nothing
        word = await FillFromDictionary(word, cancellationToken);

        var content = await _context.WordStudyContents
            .FirstOrDefaultAsync(c => c.WordId == request.WordId, cancellationToken);

        var examples = await ExamplesOf(word.Id, cancellationToken);
        var gaps = _resolver.FindGaps(word, content, examples);

        // Never without a row: a word with none is still missing its connections
        if (gaps == StudyMaterialGaps.None && content is not null)
        {
            return await NothingLeftToDo(content, cancellationToken);
        }

        if (content is not null && !TryClaim(content, now, out var refusal))
        {
            return refusal;
        }

        if (content is null)
        {
            // The version stays at its default until a generation succeeds, so a failed first
            // attempt is still asked for everything the next time
            content = new WordStudyContent
            {
                WordId = word.Id,
                Status = StudyContentStatus.Pending,
                ClaimedAtUtc = now
            };
            _context.WordStudyContents.Add(content);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another run inserted the row between the read and the write. It holds the
            // claim, so this one steps aside rather than generating the same content twice.
            return EnrichmentOutcome.ClaimedElsewhere;
        }

        return await GenerateAsync(word, content, examples, gaps, cancellationToken);
    }

    /// <summary>The word's stored examples, and the forms it has been met in.</summary>
    private async Task<StudyExampleSet> ExamplesOf(int wordId, CancellationToken cancellationToken)
    {
        var examples = await _context.StudyExamples
            .Where(e => e.WordId == wordId)
            .ToListAsync(cancellationToken);

        var forms = await _context.WordEncounters
            .Where(e => e.WordId == wordId && e.Form != null)
            .Select(e => e.Form!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new StudyExampleSet(examples, forms);
    }

    /// <summary>
    /// A word whose gaps have since been closed - usually because the dictionary data was
    /// filled in elsewhere - is marked done without spending a call.
    /// </summary>
    /// <remarks>
    /// Only reached with a row: a word without one is always missing its connections, so it
    /// is generated for rather than arriving here. That generation is what gives a word the
    /// dictionary could not fill its row, and the row is what stops the study queue asking
    /// for it on every session.
    /// </remarks>
    private async Task<EnrichmentOutcome> NothingLeftToDo(
        WordStudyContent content, CancellationToken cancellationToken)
    {
        if (content.Status != StudyContentStatus.Ready)
        {
            content.Status = StudyContentStatus.Ready;
            content.ClaimedAtUtc = null;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return EnrichmentOutcome.AlreadyFilled;
    }

    private bool TryClaim(WordStudyContent content, DateTime now, out EnrichmentOutcome refusal)
    {
        refusal = default;

        if (content.GenerationAttempts >= _options.EnrichmentMaxAttempts)
        {
            // Repeated failures stop costing calls; the word is simply skipped.
            content.Status = StudyContentStatus.Failed;
            refusal = EnrichmentOutcome.GivenUp;
            return false;
        }

        var claimIsFresh = content.Status == StudyContentStatus.Pending
            && content.ClaimedAtUtc is { } claimedAt
            && now - claimedAt < TimeSpan.FromMinutes(_options.EnrichmentStaleClaimMinutes);

        if (claimIsFresh)
        {
            refusal = EnrichmentOutcome.ClaimedElsewhere;
            return false;
        }

        content.Status = StudyContentStatus.Pending;
        content.ClaimedAtUtc = now;
        return true;
    }

    private async Task<EnrichmentOutcome> GenerateAsync(
        Word word,
        WordStudyContent content,
        StudyExampleSet examples,
        StudyMaterialGaps gaps,
        CancellationToken cancellationToken)
    {
        GeneratedStudyContent? generated;
        var meaning = _resolver.Resolve(word, content, examples).Meaning;
        var forms = _resolver.UncoveredForms(word, examples);

        try
        {
            var response = await _gptClient.SendMessageAsync(StudyContentPrompt.For(word, gaps, forms, meaning));
            generated = Parse(response);
        }
        catch (Exception ex)
        {
            return await RecordFailure(content, ex.Message, cancellationToken);
        }

        if (generated is null)
        {
            return await RecordFailure(content, "The model returned nothing usable.", cancellationToken);
        }

        if (gaps.HasFlag(StudyMaterialGaps.Meaning) && !string.IsNullOrWhiteSpace(generated.Definition))
        {
            content.GeneratedDefinition = generated.Definition.Trim();
        }

        var added = AddExamples(word, examples, generated.Examples);

        if (gaps.HasFlag(StudyMaterialGaps.Connections))
        {
            content.Usage = Short(generated.Usage);
            content.Etymology = Short(generated.Etymology);
            content.Cognates = Short(generated.Cognates);
            content.Mnemonic = Short(generated.Mnemonic);
            (content.Collocates, content.CollocateTranslations) = Phrases(generated.Collocates, 6);
            (content.NonCollocates, content.NonCollocateTranslations) = Phrases(generated.NonCollocates, 3);
        }

        // A word needs a meaning to be studied at all. Examples that came back unusable, or
        // a field the model had nothing for, only cost the word that much - not worth a
        // retry, and a retry would likely say the same.
        var withAdded = examples with { Examples = examples.Examples.Concat(added).ToList() };
        var remaining = _resolver.FindGaps(word, content, withAdded);

        if (remaining.HasFlag(StudyMaterialGaps.Meaning))
        {
            return await RecordFailure(content, "No usable definition was produced.", cancellationToken);
        }

        // Only now, so a failed generation is asked for everything again on its retry
        if (gaps.HasFlag(StudyMaterialGaps.Connections))
        {
            content.PromptVersion = StudyContentPrompt.Version;
        }

        content.Status = StudyContentStatus.Ready;
        content.ClaimedAtUtc = null;
        content.LastError = null;
        await _context.SaveChangesAsync(cancellationToken);

        return EnrichmentOutcome.Generated;
    }

    /// <summary>
    /// Keeps the generated examples that really contain the form they name - a sentence that
    /// paraphrased the word away has nothing for a cloze to blank - and are not already there.
    /// </summary>
    private List<StudyExample> AddExamples(
        Word word, StudyExampleSet existing, IReadOnlyList<GeneratedExample>? generated)
    {
        var added = new List<StudyExample>();

        foreach (var example in generated ?? (IReadOnlyList<GeneratedExample>)Array.Empty<GeneratedExample>())
        {
            var sentence = example.Sentence?.Trim();
            var form = string.IsNullOrWhiteSpace(example.Form) ? word.Headword : example.Form.Trim();

            if (string.IsNullOrEmpty(sentence) || sentence.Length > 500 || form.Length > 100
                || !HeadwordText.Contains(sentence, form))
            {
                continue;
            }

            var duplicate = existing.Examples.Concat(added)
                .Any(e => string.Equals(e.Sentence, sentence, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                continue;
            }

            var stored = new StudyExample
            {
                WordId = word.Id,
                Sentence = sentence,
                Translation = Short(example.Translation, 500),
                Form = form,
                Collocation = Short(example.Collocation, 100)
            };

            _context.StudyExamples.Add(stored);
            added.Add(stored);
        }

        return added;
    }

    /// <summary>
    /// Short phrases only, each once, and no more than are wanted - null when none are left,
    /// so an exercise that needs them is not offered. Each keeps its translation beside it,
    /// empty where the model gave none, so the two lists stay paired by position.
    /// </summary>
    private static (List<string>? Phrases, List<string>? Translations) Phrases(
        IEnumerable<GeneratedPhrase?>? values, int max)
    {
        var kept = (values ?? Enumerable.Empty<GeneratedPhrase?>())
            .Select(v => (Phrase: v?.Phrase?.Trim(), Translation: Short(v?.Translation, 60)))
            .Where(v => !string.IsNullOrEmpty(v.Phrase) && v.Phrase.Length <= 40)
            .DistinctBy(v => v.Phrase!, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

        return kept.Count == 0
            ? (null, null)
            : (kept.Select(v => v.Phrase!).ToList(), kept.Select(v => v.Translation ?? string.Empty).ToList());
    }

    /// <summary>Trimmed, empty as null, and cut short rather than stored at any length.</summary>
    private static string? Short(string? value, int max = 400)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private async Task<EnrichmentOutcome> RecordFailure(
        WordStudyContent content, string error, CancellationToken cancellationToken)
    {
        content.GenerationAttempts++;
        content.LastError = error;
        content.ClaimedAtUtc = null;

        var givenUp = content.GenerationAttempts >= _options.EnrichmentMaxAttempts;
        content.Status = givenUp ? StudyContentStatus.Failed : StudyContentStatus.Pending;

        await _context.SaveChangesAsync(cancellationToken);

        return givenUp ? EnrichmentOutcome.GivenUp : EnrichmentOutcome.Failed;
    }

    private static GeneratedStudyContent? Parse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        var json = ExtractJsonObject(response);

        try
        {
            return json is null
                ? null
                : JsonSerializer.Deserialize<GeneratedStudyContent>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Models wrap JSON in prose or code fences often enough to be worth handling, so the
    /// outermost braces are taken rather than trusting the whole response to be clean.
    /// </summary>
    private static string? ExtractJsonObject(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');

        return start >= 0 && end > start ? response[start..(end + 1)] : null;
    }

    private record GeneratedStudyContent(
        string? Definition,
        string? Usage,
        List<GeneratedExample>? Examples,
        string? Etymology,
        string? Cognates,
        string? Mnemonic,
        List<GeneratedPhrase?>? Collocates,
        List<GeneratedPhrase?>? NonCollocates);

    /// <summary>A partner word and what it means. Read from a plain string as well - the shape before translations.</summary>
    [JsonConverter(typeof(GeneratedPhraseConverter))]
    private record GeneratedPhrase(string? Phrase, string? Translation);

    private sealed class GeneratedPhraseConverter : JsonConverter<GeneratedPhrase>
    {
        public override GeneratedPhrase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return new GeneratedPhrase(reader.GetString(), null);

                case JsonTokenType.StartObject:
                {
                    using var document = JsonDocument.ParseValue(ref reader);
                    string? phrase = null, translation = null;

                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        if (property.Name.Equals("phrase", StringComparison.OrdinalIgnoreCase))
                        {
                            phrase = property.Value.GetString();
                        }
                        else if (property.Name.Equals("translation", StringComparison.OrdinalIgnoreCase))
                        {
                            translation = property.Value.GetString();
                        }
                    }

                    return new GeneratedPhrase(phrase, translation);
                }

                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, GeneratedPhrase value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }

    private record GeneratedExample(string? Sentence, string? Translation, string? Form, string? Collocation);
}
