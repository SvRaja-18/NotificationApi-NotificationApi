using Microsoft.Extensions.Options;

namespace NotificationApi.Tests.TestHelpers;

public class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
{
    public T CurrentValue { get; private set; }

    public TestOptionsMonitor(T currentValue)
    {
        CurrentValue = currentValue;
    }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
