using ShopFlow.Application.Abstractions;

namespace ShopFlow.UnitTests.Fakes;

public sealed class InMemoryCartStore : ICartStore
{
    private readonly Dictionary<Guid, Dictionary<Guid, int>> _carts = new();

    public Task<IReadOnlyDictionary<Guid, int>> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(
            _carts.TryGetValue(userId, out var cart) ? new Dictionary<Guid, int>(cart) : new Dictionary<Guid, int>());

    public Task SetQuantityAsync(Guid userId, Guid productId, int quantity, CancellationToken cancellationToken)
    {
        if (!_carts.TryGetValue(userId, out var cart))
            _carts[userId] = cart = new Dictionary<Guid, int>();
        cart[productId] = quantity;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        if (_carts.TryGetValue(userId, out var cart))
            cart.Remove(productId);
        return Task.CompletedTask;
    }

    public Task ClearAsync(Guid userId, CancellationToken cancellationToken)
    {
        _carts.Remove(userId);
        return Task.CompletedTask;
    }
}
