using System.Text;
using System.Text.RegularExpressions;

namespace VocabularyBuilder.Domain.Helpers;

/// <summary>
/// What the rules were able to make of a term taken from an import source.
/// </summary>
public enum TermVerdict
{
    /// <summary>The rules reached a headword on their own; no model call is needed.</summary>
    Lemma,

    /// <summary>
    /// A vocabulary item the rules cannot reduce - a conjugated verb, or a compound
    /// whose head is not the first word. Worth a model call.
    /// </summary>
    NeedsAnalysis,

    /// <summary>A sentence or a clause. Not a vocabulary item, and not worth a model call.</summary>
    NotVocabulary
}

/// <param name="SourceTerm">The term as the import source wrote it, kept for the encounter record.</param>
/// <param name="Lemma">The headword, set only for <see cref="TermVerdict.Lemma"/>.</param>
/// <param name="Candidate">
/// The term with its article stripped and its punctuation cleaned, which is what a model
/// should be asked about. Set for <see cref="TermVerdict.NeedsAnalysis"/>.
/// </param>
/// <param name="InflectedForm">
/// The single word a conjugated verb inflects - "allez" out of "vous allez". Set only when
/// a subject pronoun proved what follows it is a verb, which is what makes it safe to look
/// up in an inflection table; null for everything else.
/// </param>
/// <param name="Reason">Why a term was set aside, for the import report.</param>
public record FrenchTermAnalysis(
    string SourceTerm,
    TermVerdict Verdict,
    string? Lemma = null,
    string? Candidate = null,
    string? InflectedForm = null,
    string? Reason = null);

/// <summary>
/// Reduces a term as an import source wrote it - "une conférence", "Vous allez",
/// "Quel temps fait-il" - towards the headword the dictionary is keyed by.
/// </summary>
/// <remarks>
/// Deliberately rules only, and deliberately conservative: it settles the cases that are
/// unambiguous in writing (a noun behind its article, a sentence) and hands everything else
/// on for analysis rather than guessing. Headwords are stored bare - the article is derived
/// from gender by <see cref="FrenchArticles"/> - so stripping it here is what makes
/// "une randonnée" and "la randonnée" land on one word.
/// </remarks>
public static class FrenchTermNormalizer
{
    /// <summary>
    /// Articles and partitives a noun is quoted with. Longest first, so that "de la" is
    /// taken whole rather than leaving "la" behind.
    /// </summary>
    private static readonly string[] Articles =
    {
        "de la", "de l'", "des", "du", "une", "un", "les", "le", "la", "l'", "aux", "au"
    };

    /// <summary>
    /// Subject pronouns. A pronoun in front of exactly one word marks a conjugated verb
    /// ("tu chantes"); in front of more, it marks a clause ("il fait beau").
    /// </summary>
    private static readonly string[] SubjectPronouns =
    {
        "je", "j'", "tu", "il", "elle", "on", "nous", "vous", "ils", "elles", "ce", "c'", "ça"
    };

    /// <summary>
    /// Words a French question opens with. Their presence anywhere in the term is enough:
    /// they do not begin vocabulary items, only sentences.
    /// </summary>
    private static readonly string[] InterrogativeMarkers =
    {
        "qu'est-ce", "est-ce", "quel", "quelle", "quels", "quelles", "comment", "pourquoi",
        "combien", "où", "quoi", "voici", "voilà"
    };

    /// <summary>
    /// Beyond this many words a term is prose, whatever it is made of. Set above the longest
    /// compounds that are genuinely learned as units ("le tir à l'arc", "la vente à emporter").
    /// </summary>
    private const int MaxVocabularyWords = 4;

    public static FrenchTermAnalysis Analyse(string? sourceTerm)
    {
        var original = sourceTerm ?? string.Empty;
        var term = Clean(original);

        if (term.Length == 0)
        {
            return new FrenchTermAnalysis(original, TermVerdict.NotVocabulary, Reason: "Empty term");
        }

        // A question mark survives Clean only as evidence; it is stripped below
        if (original.Contains('?'))
        {
            return new FrenchTermAnalysis(original, TermVerdict.NotVocabulary, Reason: "Question");
        }

        // Length is judged before elisions are separated, so that "le tir à l'arc" counts
        // as the four words it is written as rather than the five it is worked on as
        if (term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > MaxVocabularyWords)
        {
            return new FrenchTermAnalysis(original, TermVerdict.NotVocabulary, Reason: "Sentence");
        }

        var words = Split(term);

        if (words.Any(word => InterrogativeMarkers.Contains(word, StringComparer.OrdinalIgnoreCase)))
        {
            return new FrenchTermAnalysis(original, TermVerdict.NotVocabulary, Reason: "Question");
        }

        // A pronoun tells us what follows it is a verb, so the two cases split on how much
        // follows: one word is the verb alone, more is the verb with its complement
        if (SubjectPronouns.Contains(words[0], StringComparer.OrdinalIgnoreCase))
        {
            var rest = words.Skip(1).ToArray();

            return rest.Length switch
            {
                0 => new FrenchTermAnalysis(original, TermVerdict.NotVocabulary, Reason: "Pronoun only"),
                1 => new FrenchTermAnalysis(
                    original,
                    TermVerdict.NeedsAnalysis,
                    Candidate: Join(words),
                    InflectedForm: rest[0],
                    Reason: "Conjugated verb"),
                _ => new FrenchTermAnalysis(original, TermVerdict.NotVocabulary, Reason: "Clause")
            };
        }

        var core = StripArticle(words);

        if (core.Length == 0)
        {
            return new FrenchTermAnalysis(original, TermVerdict.NotVocabulary, Reason: "Article only");
        }

        // One word behind an article is the case the rules exist for
        if (core.Length == 1)
        {
            return new FrenchTermAnalysis(original, TermVerdict.Lemma, Lemma: core[0]);
        }

        return new FrenchTermAnalysis(
            original,
            TermVerdict.NeedsAnalysis,
            Candidate: Join(core),
            Reason: "Compound");
    }

    /// <summary>
    /// Removes a leading article from an already-split term. Public because an answer
    /// coming back from a model is quoted with its article as often as not.
    /// </summary>
    public static string StripArticle(string term)
    {
        var words = Split(Clean(term));
        var core = StripArticle(words);
        return Join(core);
    }

    private static string[] StripArticle(string[] words)
    {
        foreach (var article in Articles)
        {
            var articleWords = article.Split(' ');

            if (words.Length <= articleWords.Length)
            {
                continue;
            }

            if (words.Take(articleWords.Length).SequenceEqual(articleWords, StringComparer.OrdinalIgnoreCase))
            {
                return words.Skip(articleWords.Length).ToArray();
            }
        }

        return words;
    }

    /// <summary>
    /// Lower-cases, settles the apostrophe on the typewriter form, drops sentence
    /// punctuation and collapses whitespace. Accents and hyphens are left alone: they
    /// belong to the word.
    /// </summary>
    public static string Clean(string term)
    {
        var cleaned = term
            .Replace('’', '\'')
            .Replace('ʼ', '\'')
            .ToLowerInvariant();

        cleaned = Regex.Replace(cleaned, @"[?!.,;:""«»]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        return cleaned.Trim();
    }

    /// <summary>
    /// Puts split words back together, reattaching an elision to the word it belongs to so
    /// that "l'arc" is asked about as it is written rather than as "l' arc".
    /// </summary>
    private static string Join(IReadOnlyList<string> words)
    {
        var joined = new StringBuilder();

        foreach (var word in words)
        {
            if (joined.Length > 0 && joined[^1] != '\'')
            {
                joined.Append(' ');
            }

            joined.Append(word);
        }

        return joined.ToString();
    }

    /// <summary>
    /// Splits into words, keeping an elided article attached to nothing: "l'arbre" has to
    /// become "l'" and "arbre" for the article to be strippable, while "aujourd'hui" and
    /// "qu'est-ce" must stay whole.
    /// </summary>
    private static string[] Split(string term)
    {
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        foreach (var word in words)
        {
            var apostrophe = word.IndexOf('\'');

            // Only a one-letter prefix elides - "l'", "d'", "j'", "c'". A longer one
            // is part of the word
            if (apostrophe == 1 && word.Length > 2)
            {
                result.Add(word[..2]);
                result.Add(word[2..]);
                continue;
            }

            result.Add(word);
        }

        return result.ToArray();
    }
}
