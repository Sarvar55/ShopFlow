using ShopFlow.Application.Abstractions;
using StackExchange.Redis;

namespace ShopFlow.Infrastructure.Caching;

/// <summary>
/// Each cart is a Redis hash: key "shopflow:cart:{userId}", field = product id, value = quantity.
/// Carts expire after a period of inactivity; every write refreshes the expiry.
/// </summary>
public sealed class RedisCartStore(IConnectionMultiplexer redis) : ICartStore
{
    private static readonly TimeSpan CartTtl = TimeSpan.FromDays(7);

    private IDatabase Db => redis.GetDatabase();

    private static RedisKey Key(Guid userId) => $"shopflow:cart:{userId}";

    public async Task<IReadOnlyDictionary<Guid, int>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entries = await Db.HashGetAllAsync(Key(userId));
        return entries.ToDictionary(e => Guid.Parse(e.Name.ToString()), e => (int)e.Value);
    }

    public async Task SetQuantityAsync(Guid userId, Guid productId, int quantity, CancellationToken cancellationToken)
    {
        var tx = Db.CreateTransaction();
        _ = tx.HashSetAsync(Key(userId), productId.ToString(), quantity);
        _ = tx.KeyExpireAsync(Key(userId), CartTtl);
        await tx.ExecuteAsync();
    }

    public Task RemoveAsync(Guid userId, Guid productId, CancellationToken cancellationToken) =>
        Db.HashDeleteAsync(Key(userId), productId.ToString());

    public Task ClearAsync(Guid userId, CancellationToken cancellationToken) =>
        Db.KeyDeleteAsync(Key(userId));
}
