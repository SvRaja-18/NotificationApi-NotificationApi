namespace NotificationApi.Tests.TestHelpers;

public class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public TestTimeProvider(DateTimeOffset initialUtcNow)
    {
        _utcNow = initialUtcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}
