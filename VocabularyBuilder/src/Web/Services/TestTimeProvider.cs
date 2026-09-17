namespace VocabularyBuilder.Web.Services;

/// <summary>
/// A clock the tests can move.
///
/// Spaced repetition is measured in days, so most of the behaviour worth testing - a word
/// coming back the next morning, a rung unlocking after a week, a probe escalating after a
/// long absence - is unreachable in a test suite that can only wait in real time.
///
/// Registered only in the E2ETest environment; every other environment keeps the system
/// clock.
/// </summary>
public class TestTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private TimeSpan _offset;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return DateTimeOffset.UtcNow + _offset;
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _offset += by;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _offset = TimeSpan.Zero;
        }
    }

    public TimeSpan Offset
    {
        get
        {
            lock (_gate)
            {
                return _offset;
            }
        }
    }
}
