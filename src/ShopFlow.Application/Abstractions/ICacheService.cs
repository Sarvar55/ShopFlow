namespace ShopFlow.Application.Abstractions;

/// <summary>
/// Best-effort distributed cache. Implementations must never throw on cache failures:
/// a cache outage should degrade performance, not availability.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken);
    Task<long> GetCounterAsync(string key, CancellationToken cancellationToken);
    Task IncrementAsync(string key, CancellationToken cancellationToken);
}
