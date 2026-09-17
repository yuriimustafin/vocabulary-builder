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
/// Rules first, model second. <see cref="FrenchTermNormalizer"/> settles the two
/// unambiguous cases - a noun behind its article, and prose - without a request; only what
/// is left over is sent for analysis. On a typical exported list that is a small minority
/// of the rows, which is what keeps a few hundred terms down to a handful of model calls.
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

    public ResolveVocabularyTermsQueryHandler(IVocabularyAnalyzer analyzer)
    {
        _analyzer = analyzer;
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
}
