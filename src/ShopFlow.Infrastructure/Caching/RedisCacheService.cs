using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShopFlow.Application.Abstractions;
using StackExchange.Redis;

namespace ShopFlow.Infrastructure.Caching;

public sealed class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private const string Prefix = "shopflow:cache:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IDatabase Db => redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var value = await Db.StringGetAsync(Prefix + key);
            return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogWarning(ex, "Cache read failed for {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            await Db.StringSetAsync(Prefix + key, JsonSerializer.Serialize(value, JsonOptions), ttl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Cache write failed for {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        var redisKeys = keys.Select(k => (RedisKey)(Prefix + k)).ToArray();
        if (redisKeys.Length == 0)
            return;

        try
        {
            await Db.KeyDeleteAsync(redisKeys);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Cache invalidation failed for {CacheKeys}", string.Join(",", redisKeys));
        }
    }

    public async Task<long> GetCounterAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var value = await Db.StringGetAsync(Prefix + key);
            return value.TryParse(out long counter) ? counter : 0;
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Cache counter read failed for {CacheKey}", key);
            return 0;
        }
    }

    public async Task IncrementAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await Db.StringIncrementAsync(Prefix + key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Cache counter increment failed for {CacheKey}", key);
        }
    }
}
