using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Entities.History;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.History;

/// <summary>
/// Adds entries to the activity log through the handler's own context.
/// </summary>
/// <remarks>
/// The entry is added, not saved: it goes out with the handler's next SaveChanges, in the same
/// transaction as the change it describes. That is the whole reason for writing it this way -
/// an edit that fails to save leaves no entry claiming it happened, and one that saves cannot
/// lose its entry. So call it before the handler's last save, never after.
///
/// A word's id is only known once the word has been saved, which is why a handler creating
/// one records it between its first save and its second.
/// </remarks>
public static class ActivityRecording
{
    private static readonly JsonSerializerOptions DetailsJson = new()
    {
        // Headwords are French as often as not; "é" in the log helps nobody
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ActivityLogEntry RecordActivity(
        this IApplicationDbContext context,
        ActivityAction action,
        Word? word = null,
        string? summary = null,
        object? details = null,
        Language? language = null,
        int? wordId = null,
        string? headword = null,
        int? listId = null,
        int? importId = null)
    {
        var entry = new ActivityLogEntry
        {
            OccurredAtUtc = DateTime.UtcNow,
            Action = action,
            Language = language ?? word?.Language,
            WordId = wordId ?? (word is { Id: > 0 } ? word.Id : null),
            Headword = headword ?? word?.Headword,
            ListId = listId,
            ImportId = importId,
            Summary = Truncate(summary, 500),
            Details = details is null ? null : JsonSerializer.Serialize(details, DetailsJson)
        };

        context.ActivityLog.Add(entry);

        return entry;
    }

    /// <summary>
    /// The fields that differ between two snapshots of the same thing, as
    /// <c>{ field: { from, to } }</c>, or null when nothing changed.
    /// </summary>
    public static Dictionary<string, object?>? Changes(IReadOnlyDictionary<string, object?> before, IReadOnlyDictionary<string, object?> after)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var (field, was) in before)
        {
            var now = after.GetValueOrDefault(field);

            if (!Equal(was, now))
            {
                changes[field] = new { from = was, to = now };
            }
        }

        return changes.Count > 0 ? changes : null;
    }

    private static bool Equal(object? a, object? b)
    {
        if (a is IEnumerable<string> left && b is IEnumerable<string> right)
        {
            return left.SequenceEqual(right);
        }

        return Equals(a, b);
    }

    private static string? Truncate(string? value, int length) =>
        value is null || value.Length <= length ? value : value[..(length - 1)] + "…";
}
