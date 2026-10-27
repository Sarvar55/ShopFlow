using ShopFlow.Application.Abstractions;

namespace ShopFlow.UnitTests.Fakes;

public sealed class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, object?> _values = new();
    private readonly Dictionary<string, long> _counters = new();

    public int Hits { get; private set; }

    public bool Contains(string key) => _values.ContainsKey(key);

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        if (_values.TryGetValue(key, out var value))
        {
            Hits++;
            return Task.FromResult((T?)value);
        }
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
            _values.Remove(key);
        return Task.CompletedTask;
    }

    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_counters.GetValueOrDefault(key));

    public Task IncrementAsync(string key, CancellationToken cancellationToken)
    {
        _counters[key] = _counters.GetValueOrDefault(key) + 1;
        return Task.CompletedTask;
    }
}
