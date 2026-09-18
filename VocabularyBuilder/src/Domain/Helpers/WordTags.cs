namespace VocabularyBuilder.Domain.Helpers;

/// <summary>
/// Reading and combining the labels a word is collected under.
/// </summary>
/// <remarks>
/// Tags accumulate. A word met again under a second tag keeps the first, because the point
/// of them is to record every place the word has turned up, not the latest one.
/// </remarks>
public static class WordTags
{
    /// <summary>
    /// Splits what someone typed into one field into separate tags. A single tag is the
    /// normal case, but "preply, french" is what people write when they mean two, so the
    /// comma is honoured rather than swallowed into one long tag.
    /// </summary>
    public static List<string> Parse(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return new List<string>();
        }

        return Distinct(field.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// The tags a word should end up with: the ones it already had, in the order it had
    /// them, followed by any that are new. Comparison ignores case, so re-importing under
    /// "Preply" does not add a second tag beside "preply".
    /// </summary>
    public static List<string> Merge(IEnumerable<string>? existing, IEnumerable<string>? added)
    {
        return Distinct((existing ?? Enumerable.Empty<string>())
            .Concat(added ?? Enumerable.Empty<string>()));
    }

    private static List<string> Distinct(IEnumerable<string> tags)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var tag in tags)
        {
            var trimmed = tag.Trim();

            if (trimmed.Length > 0 && seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
