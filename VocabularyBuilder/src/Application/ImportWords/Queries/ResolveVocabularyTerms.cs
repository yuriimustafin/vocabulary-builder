using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Application.ImportWords.Queries;

/// <param name="SourceTerm">The term as the import source wrote it: "une randonnée", "Vous allez".</param>
/// <param name="Lemma">The headword it was reduced to: "randonnée", "aller".</param>
public record ResolvedTerm(string SourceTerm, string Lemma);

/// <param name="SourceTerm">The term that will not be imported.</param>
/// <param name="Reason">Why, for the import report: "Question", "Sentence", "Expression".</param>
public record SkippedTerm(string SourceTerm, string Reason);

public class VocabularyTermResolution
{
    public List<ResolvedTerm> Resolved { get; init; } = new();
    public List<SkippedTerm> Skipped { get; init; } = new();
}

/// <summary>
/// Turns terms as an import source wrote them into the headwords the vocabulary is keyed
/// by, setting aside the ones that are not vocabulary items.
/// </summary>
/// <remarks>
/// Three tiers, cheapest and steadiest first, so that a model is asked only what nothing
/// else can answer:
///
/// 1. <see cref="FrenchTermNormalizer"/> settles the two cases that are unambiguous in
///    writing - a noun behind its article, and prose.
/// 2. <see cref="LookupInflectedFormsQuery"/> reduces conjugated verbs against the imported
///    frequency data. Free, and - more importantly - it cannot drift between imports the
///    way a model's answer can, which is what keeps encounter counts adding up.
/// 3. Whatever is left goes to the model: compounds, and the verb forms the frequency data
///    cannot tell apart from a noun of the same spelling.
///
/// On a typical exported list the first two tiers settle most of the rows, which is what
/// keeps a few hundred terms down to a handful of model calls.
/// </remarks>
public record ResolveVocabularyTermsQuery : IRequest<VocabularyTermResolution>
{
    public required IReadOnlyList<string> Terms { get; init; }
    public Language Language { get; init; } = Language.French;
}

public class ResolveVocabularyTermsQueryHandler
    : IRequestHandler<ResolveVocabularyTermsQuery, VocabularyTermResolution>
{
    private readonly IVocabularyAnalyzer _analyzer;
    private readonly ISender _sender;

    public ResolveVocabularyTermsQueryHandler(IVocabularyAnalyzer analyzer, ISender sender)
    {
        _analyzer = analyzer;
        _sender = sender;
    }

    public async Task<VocabularyTermResolution> Handle(
        ResolveVocabularyTermsQuery request,
        CancellationToken cancellationToken)
    {
        var result = new VocabularyTermResolution();

        // The rules encode French grammar, so another language passes its terms straight
        // through rather than being reduced by rules that do not apply to it
        if (request.Language != Language.French)
        {
            foreach (var term in request.Terms)
            {
                var cleaned = term.Trim().ToLowerInvariant();

                if (cleaned.Length > 0)
                {
                    result.Resolved.Add(new ResolvedTerm(term, cleaned));
                }
            }

            return result;
        }

        var analyses = request.Terms.Select(FrenchTermNormalizer.Analyse).ToList();

        foreach (var analysis in analyses.Where(a => a.Verdict == TermVerdict.Lemma))
        {
            result.Resolved.Add(new ResolvedTerm(analysis.SourceTerm, analysis.Lemma!));
        }

        foreach (var analysis in analyses.Where(a => a.Verdict == TermVerdict.NotVocabulary))
        {
            result.Skipped.Add(new SkippedTerm(analysis.SourceTerm, analysis.Reason ?? "Not a vocabulary item"));
        }

        var needAnalysis = analyses.Where(a => a.Verdict == TermVerdict.NeedsAnalysis).ToList();

        if (needAnalysis.Count == 0)
        {
            return result;
        }

        needAnalysis = await ResolveFromFrequencyData(needAnalysis, request, result, cancellationToken);

        if (needAnalysis.Count == 0)
        {
            return result;
        }

        // Asked about the cleaned candidate, but reported against the original term, which
        // is what the encounter record has to name
        var candidates = needAnalysis
            .Select(a => a.Candidate!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var analyzed = await _analyzer.ResolveLemmasAsync(candidates, request.Language, cancellationToken);

        var byCandidate = analyzed
            .GroupBy(a => a.SourceTerm.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var analysis in needAnalysis)
        {
            if (!byCandidate.TryGetValue(analysis.Candidate!.Trim(), out var answer))
            {
                result.Skipped.Add(new SkippedTerm(analysis.SourceTerm, "Could not be analysed"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(answer.Lemma))
            {
                result.Skipped.Add(new SkippedTerm(analysis.SourceTerm, answer.SkipReason ?? "Not a vocabulary item"));
                continue;
            }

            // A model regularly answers with the article attached however firmly it is
            // asked not to, and a headword carrying one would never match an existing word
            var lemma = FrenchTermNormalizer.StripArticle(answer.Lemma);

            if (string.IsNullOrWhiteSpace(lemma))
            {
                result.Skipped.Add(new SkippedTerm(analysis.SourceTerm, "Analysis returned no headword"));
                continue;
            }

            result.Resolved.Add(new ResolvedTerm(analysis.SourceTerm, lemma));
        }

        return result;
    }

    /// <summary>
    /// Settles the conjugated verbs the frequency data can reduce on its own, and returns
    /// the terms still needing a model.
    /// </summary>
    private async Task<List<FrenchTermAnalysis>> ResolveFromFrequencyData(
        List<FrenchTermAnalysis> needAnalysis,
        ResolveVocabularyTermsQuery request,
        VocabularyTermResolution result,
        CancellationToken cancellationToken)
    {
        // Only the forms a pronoun proved to be verbs are looked up; the same table would
        // reduce plural nouns wrongly, which LookupInflectedFormsQuery explains
        var forms = needAnalysis
            .Where(a => a.InflectedForm != null)
            .Select(a => a.InflectedForm!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (forms.Count == 0)
        {
            return needAnalysis;
        }

        var lemmas = await _sender.Send(new LookupInflectedFormsQuery
        {
            Forms = forms,
            Language = request.Language
        }, cancellationToken);

        if (lemmas.Count == 0)
        {
            return needAnalysis;
        }

        var unresolved = new List<FrenchTermAnalysis>();

        foreach (var analysis in needAnalysis)
        {
            if (analysis.InflectedForm != null && lemmas.TryGetValue(analysis.InflectedForm, out var lemma))
            {
                result.Resolved.Add(new ResolvedTerm(analysis.SourceTerm, lemma));
                continue;
            }

            unresolved.Add(analysis);
        }

        return unresolved;
    }
}
