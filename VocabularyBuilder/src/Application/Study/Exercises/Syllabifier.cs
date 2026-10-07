using System.Globalization;
using System.Text;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

/// <summary>
/// Splits a word into written syllables, for the syllable scramble.
///
/// The tiles only need to be chunks a learner would recognise as pieces of the word, not a
/// dictionary's hyphenation, so this is a small set of rules rather than a pattern file:
/// <list type="bullet">
/// <item>Vowel groups (ou, ai, eau, oi, ea, oo...) stay together; a tréma, or a French
/// é followed by another vowel, splits them (ha-ïr, cré-a-tion).</item>
/// <item>Digraphs are one consonant (ch, ph, th, gn, qu; sh, ck, ng in English).</item>
/// <item>One consonant between vowels starts the next syllable (cho-co-lat).</item>
/// <item>An obstruent followed by l or r starts the next syllable together (a-bri-cot,
/// ta-ble); any other pair splits between them (ar-bre, at-ten-tion, win-dow).</item>
/// <item>In French a final mute -e or -es after a single consonant joins the syllable
/// before (ma-gni-fique). In English a silent final e does, except in consonant + le
/// (lit-tle), and so does -ed except after t or d.</item>
/// </list>
/// The pieces always join back into the word exactly, spaces and hyphens aside.
/// </summary>
public static class Syllabifier
{
    private const string Vowels = "aeiouyàâäéèêëîïôöùûüÿœæ";
    private const string Trema = "ëïüÿ";
    private const string Obstruents = "bcdfgkptv";

    private static readonly string[] FrenchDigraphs = { "ch", "ph", "th", "gn" };
    private static readonly string[] EnglishDigraphs = { "ch", "sh", "th", "ph", "wh", "ck", "ng" };

    /// <summary>
    /// The syllables of a word or phrase, in order. Each part of a phrase is split on its
    /// own; an elided article (l', d') stays on the front of the word it belongs to.
    /// </summary>
    public static IReadOnlyList<string> Split(string text, Language language)
    {
        var syllables = new List<string>();

        foreach (var part in Parts(text))
        {
            syllables.AddRange(SplitWord(part, language));
        }

        return syllables;
    }

    /// <summary>
    /// The words of a phrase, with spaces and hyphens dropped - they are not part of what
    /// is being assembled - and an elision kept with the word after it.
    /// </summary>
    /// <summary>Fewest pieces a word is put back together from, unless it is too short for them.</summary>
    public const int FewestChunks = 3;

    /// <summary>Most pieces: past four, putting them in order is a search rather than a recall.</summary>
    public const int MostChunks = 4;

    /// <summary>Pairs of letters a chunk boundary avoids when it can - a digraph or a vowel sound.</summary>
    private static readonly HashSet<string> KeptTogether = new(StringComparer.Ordinal)
    {
        "qu", "ch", "ph", "th", "gn", "sh", "ck", "ng", "wh",
        "ou", "ai", "au", "ei", "eu", "oi", "oo", "ee", "ea", "ie", "ue"
    };

    /// <summary>
    /// The pieces a word is put back together from: three or four, its syllables wherever
    /// they make that many - a longer syllable split, the shortest neighbours joined, until
    /// they do. A word of three letters or fewer is simply its letters. The pieces always
    /// join back into the word exactly, spaces and hyphens aside.
    /// </summary>
    public static IReadOnlyList<string> Chunks(string text, Language language)
    {
        var letters = Letters(text);

        if (letters.Count <= FewestChunks)
        {
            return letters;
        }

        var chunks = Split(text, language).ToList();

        while (chunks.Count < FewestChunks && SplitLongest(chunks))
        {
        }

        while (chunks.Count > MostChunks)
        {
            // The neighbours that make the shortest piece together
            var join = Enumerable.Range(0, chunks.Count - 1)
                .OrderBy(i => Length(chunks[i]) + Length(chunks[i + 1]))
                .ThenBy(i => i)
                .First();

            chunks[join] += chunks[join + 1];
            chunks.RemoveAt(join + 1);
        }

        return chunks;
    }

    /// <summary>
    /// Splits the longest piece that can be split in two, as near its middle as possible
    /// without parting a digraph or a vowel sound. False when every piece is a single letter.
    /// </summary>
    private static bool SplitLongest(List<string> chunks)
    {
        var index = Enumerable.Range(0, chunks.Count)
            .Where(i => Length(chunks[i]) > 1)
            .OrderByDescending(i => Length(chunks[i]))
            .ThenBy(i => i)
            .DefaultIfEmpty(-1)
            .First();

        if (index < 0)
        {
            return false;
        }

        var elements = Elements(chunks[index]);
        var middle = elements.Count / 2.0;

        var at = Enumerable.Range(1, elements.Count - 1)
            .OrderBy(i => KeptTogether.Contains((elements[i - 1] + elements[i]).ToLowerInvariant()) ? 1 : 0)
            .ThenBy(i => Math.Abs(i - middle))
            .ThenByDescending(i => i)
            .First();

        chunks[index] = string.Concat(elements.Take(at));
        chunks.Insert(index + 1, string.Concat(elements.Skip(at)));
        return true;
    }

    /// <summary>The letters of a word or phrase as written, without its spaces and hyphens.</summary>
    private static List<string> Letters(string text) =>
        Elements(text).Where(e => !string.IsNullOrWhiteSpace(e) && e != "-").ToList();

    private static List<string> Elements(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text.Trim().Normalize(NormalizationForm.FormC));

        while (enumerator.MoveNext())
        {
            elements.Add((string)enumerator.Current);
        }

        return elements;
    }

    private static int Length(string chunk) => Elements(chunk).Count;

    private static IEnumerable<string> Parts(string text)
    {
        var pending = string.Empty;

        foreach (var raw in text.Trim().Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = pending + raw;
            pending = string.Empty;

            var apostrophe = word.LastIndexOfAny(new[] { '\'', '’' });

            if (apostrophe == word.Length - 1)
            {
                // A bare "l'" or "d'" written apart from its word: carry it over.
                pending = word;
                continue;
            }

            yield return word;
        }

        if (pending.Length > 0)
        {
            yield return pending;
        }
    }

    private static List<string> SplitWord(string word, Language language)
    {
        var units = Units(word, language);
        var nuclei = Nuclei(units, language);

        if (nuclei.Count <= 1)
        {
            return new List<string> { word };
        }

        // Where each syllable after the first begins, as a unit index.
        var starts = new List<int>();

        for (var n = 1; n < nuclei.Count; n++)
        {
            var clusterStart = nuclei[n - 1].End + 1;
            var clusterEnd = nuclei[n].Start - 1;
            starts.Add(BoundaryIn(units, clusterStart, clusterEnd, language));
        }

        var syllables = new List<string>();
        var from = 0;

        foreach (var start in starts)
        {
            syllables.Add(Join(units, from, start - 1));
            from = start;
        }

        syllables.Add(Join(units, from, units.Count - 1));

        return MergeSilentEnding(syllables, units, starts, nuclei, language);
    }

    /// <summary>
    /// Where the next syllable starts within the consonants between two vowels. With none,
    /// right at the second vowel; with one, on it; with more, before a trailing obstruent +
    /// l/r pair if there is one and otherwise before the last consonant.
    /// </summary>
    private static int BoundaryIn(List<Unit> units, int clusterStart, int clusterEnd, Language language)
    {
        var count = clusterEnd - clusterStart + 1;

        if (count <= 0)
        {
            return clusterStart;
        }

        if (count == 1)
        {
            // x between vowels stays behind (ex-er-cice); an English ck always does.
            var only = units[clusterStart].Lower;
            return only == "x" || only == "ck" ? clusterStart + 1 : clusterStart;
        }

        return IsOnsetPair(units[clusterEnd - 1], units[clusterEnd]) ? clusterEnd - 1 : clusterEnd;
    }

    private static bool IsOnsetPair(Unit first, Unit second) =>
        (second.Lower is "l" or "r")
        && (first.Lower.Length == 1 ? Obstruents.Contains(first.Lower[0]) : first.Lower is "ch" or "ph" or "th");

    /// <summary>
    /// A final vowel that is not pronounced is not a syllable of its own. In French that is
    /// a mute -e/-es after a single consonant; after obstruent + l/r it keeps its own tile
    /// (ta-ble, fe-nê-tre). In English a silent -e always goes back, except in consonant +
    /// le (lit-tle), and -ed does unless it follows t or d.
    /// </summary>
    private static List<string> MergeSilentEnding(
        List<string> syllables, List<Unit> units, List<int> starts, List<(int Start, int End)> nuclei, Language language)
    {
        var last = nuclei[^1];
        var tail = string.Concat(units.Skip(last.End + 1).Select(u => u.Lower));
        var nucleus = string.Concat(units.Skip(last.Start).Take(last.End - last.Start + 1).Select(u => u.Lower));
        var lastStart = starts[^1];
        var onset = units.Skip(lastStart).Take(last.Start - lastStart).Select(u => u.Lower).ToList();

        bool silent;

        if (language == Language.French)
        {
            silent = nucleus == "e" && tail is "" or "s" && onset.Count <= 1;
        }
        else
        {
            // ta-ble, lit-tle: the l has a consonant in front of it within the syllable.
            var consonantLe = nucleus == "e" && tail == "" && onset.Count >= 2 && onset[^1] == "l";
            var silentE = nucleus == "e" && tail is "" or "s" && !consonantLe;
            var silentEd = nucleus == "e" && tail == "d" && !(onset.Count == 1 && onset[0] is "t" or "d");
            silent = silentE || silentEd;
        }

        if (!silent || syllables.Count < 2)
        {
            return syllables;
        }

        syllables[^2] += syllables[^1];
        syllables.RemoveAt(syllables.Count - 1);
        return syllables;
    }

    /// <summary>
    /// The vowel groups of the word, as unit ranges. Adjacent vowels group together, except
    /// across a tréma or after a French é.
    /// </summary>
    private static List<(int Start, int End)> Nuclei(List<Unit> units, Language language)
    {
        var nuclei = new List<(int Start, int End)>();

        for (var i = 0; i < units.Count; i++)
        {
            if (!units[i].IsVowel)
            {
                continue;
            }

            var end = i;

            while (end + 1 < units.Count
                && units[end + 1].IsVowel
                && !Trema.Contains(units[end + 1].Lower[0])
                && !(language == Language.French && units[end].Lower == "é"))
            {
                end++;
            }

            nuclei.Add((i, end));
            i = end;
        }

        return nuclei;
    }

    /// <summary>
    /// The word as letters, with digraphs joined into one consonant. qu and gu before a
    /// vowel are consonants too, so the u is not mistaken for a vowel of its own. A y at the
    /// start of a word is a consonant (yeux aside, near enough).
    /// </summary>
    private static List<Unit> Units(string word, Language language)
    {
        var letters = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(word.Normalize(NormalizationForm.FormC));

        while (enumerator.MoveNext())
        {
            letters.Add((string)enumerator.Current);
        }

        var digraphs = language == Language.French ? FrenchDigraphs : EnglishDigraphs;
        var units = new List<Unit>();

        for (var i = 0; i < letters.Count; i++)
        {
            var lower = letters[i].ToLowerInvariant();
            var next = i + 1 < letters.Count ? letters[i + 1].ToLowerInvariant() : null;
            var afterNext = i + 2 < letters.Count ? letters[i + 2].ToLowerInvariant() : null;

            var joinsNext = next is not null
                && (digraphs.Contains(lower + next)
                    || (lower is "q" && next == "u")
                    || (lower is "g" && next == "u" && afterNext is not null && IsVowelLetter(afterNext)));

            if (joinsNext)
            {
                units.Add(new Unit(letters[i] + letters[i + 1], lower + next, IsVowel: false));
                i++;
                continue;
            }

            var isVowel = IsVowelLetter(lower) && !(lower == "y" && i == 0);
            units.Add(new Unit(letters[i], lower, isVowel));
        }

        return units;
    }

    private static bool IsVowelLetter(string letter) => letter.Length == 1 && Vowels.Contains(letter[0]);

    private static string Join(List<Unit> units, int from, int to) =>
        string.Concat(units.Skip(from).Take(to - from + 1).Select(u => u.Text));

    /// <param name="Text">As written, case and accents kept.</param>
    /// <param name="Lower">Lower-cased, for the rules.</param>
    private record Unit(string Text, string Lower, bool IsVowel);
}
