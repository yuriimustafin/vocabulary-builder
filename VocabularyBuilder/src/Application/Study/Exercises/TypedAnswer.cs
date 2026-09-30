using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Study.Exercises;

public enum TypedMatchKind
{
    /// <summary>The word, exactly - case, spacing and a leading article aside.</summary>
    Exact,

    /// <summary>The right letters with an accent missing or wrong.</summary>
    AccentsOnly,

    /// <summary>One letter off - missing, extra, wrong or swapped - in a word long enough to tell.</summary>
    Typo,

    /// <summary>The right word under the wrong article, a French noun given the other gender.</summary>
    WrongArticle,

    Wrong
}

/// <summary>How a typed answer compared with the word.</summary>
/// <param name="Kind">What kind of match it was.</param>
/// <param name="Word">What was typed, tidied and without any article in front of it.</param>
public record TypedMatch(TypedMatchKind Kind, string Word)
{
    /// <summary>Close enough that the word was clearly known.</summary>
    public bool Accepted => Kind != TypedMatchKind.Wrong;
}

/// <summary>
/// Marks a typed word the way a teacher would rather than a string comparison.
///
/// Case, extra spaces, hyphens against spaces and a leading article do not matter. A missing
/// accent, one slipped letter, or a French noun under the other gender's article is accepted
/// but not as clean - the word was known, the spelling or the gender was not quite - which
/// holds it where it is instead of either failing it or moving it on. Anything further off is
/// wrong. One slip is only forgiven in a word of five letters or more: in a short word a
/// single letter is usually a different word.
/// </summary>
public static class TypedAnswer
{
    private const int TypoMinLength = 5;

    private static readonly Regex FrenchArticle = new(
        @"^(?:(?<article>le|la|les|un|une|des)\s+|(?<article>l')\s*(?=\S))", RegexOptions.CultureInvariant);

    private static readonly Regex EnglishArticle = new(
        @"^(?<article>the|a|an|to)\s+", RegexOptions.CultureInvariant);

    public static TypedMatch Match(string? typed, StudyMaterial material)
    {
        var expected = Tidy(material.Headword);
        var (article, word) = SplitArticle(Tidy(typed ?? string.Empty), expected, material.Language);

        if (word.Length == 0)
        {
            return new TypedMatch(TypedMatchKind.Wrong, word);
        }

        if (word == expected)
        {
            return new TypedMatch(
                ContradictsGender(article, material) ? TypedMatchKind.WrongArticle : TypedMatchKind.Exact, word);
        }

        if (StripAccents(word) == StripAccents(expected))
        {
            return new TypedMatch(TypedMatchKind.AccentsOnly, word);
        }

        if (expected.Length >= TypoMinLength && Distance(StripAccents(word), StripAccents(expected)) <= 1)
        {
            return new TypedMatch(TypedMatchKind.Typo, word);
        }

        return new TypedMatch(TypedMatchKind.Wrong, word);
    }

    /// <summary>
    /// Lower case, composed accents, one kind of apostrophe, the ligatures spelled out, and
    /// hyphens and runs of spaces as single spaces.
    /// </summary>
    public static string Tidy(string value)
    {
        var text = value.Normalize(NormalizationForm.FormC).ToLowerInvariant()
            .Replace('’', '\'').Replace('‘', '\'').Replace('ʼ', '\'')
            .Replace("œ", "oe").Replace("æ", "ae")
            .Replace('-', ' ');

        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Takes an article off the front - unless the word itself starts with it, which is the
    /// only way to tell "la" typed before "chaise" from a headword such as "la plupart".
    /// </summary>
    private static (string? Article, string Word) SplitArticle(string typed, string expected, Language language)
    {
        var pattern = language == Language.French ? FrenchArticle : EnglishArticle;
        var match = pattern.Match(typed);

        if (!match.Success || pattern.IsMatch(expected))
        {
            return (null, typed);
        }

        return (match.Groups["article"].Value, typed[match.Length..].TrimStart());
    }

    /// <summary>
    /// Only an article that shows gender can contradict it: l', les and des do not, and a
    /// noun that takes either gender takes either article.
    /// </summary>
    private static bool ContradictsGender(string? article, StudyMaterial material)
    {
        var gender = material.Article?.Gender;

        return (article, gender) switch
        {
            ("le" or "un", "feminine") => true,
            ("la" or "une", "masculine") => true,
            _ => false
        };
    }

    public static string StripAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Edit distance where swapping two neighbouring letters costs one, like any other slip.
    /// </summary>
    public static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) { d[i, 0] = i; }
        for (var j = 0; j <= b.Length; j++) { d[0, j] = j; }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);

                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }
}
