namespace VocabularyBuilder.Application.Study;

/// <summary>
/// Where one study day ends and the next begins.
///
/// Not midnight: a session that runs past it should still count as the same day, so the
/// boundary sits in the small hours and a late-night reviewer is not handed a second
/// day's worth of new words.
/// </summary>
public static class StudyDay
{
    public static DateTime StartOf(DateTime nowUtc, int rolloverHourUtc)
    {
        var hour = Math.Clamp(rolloverHourUtc, 0, 23);
        var todaysBoundary = nowUtc.Date.AddHours(hour);

        return nowUtc >= todaysBoundary ? todaysBoundary : todaysBoundary.AddDays(-1);
    }
}
