namespace ShopFlow.Application.Abstractions;

/// <summary>Shopping cart storage (Redis). Maps product id to quantity.</summary>
public interface ICartStore
{
    Task<IReadOnlyDictionary<Guid, int>> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task SetQuantityAsync(Guid userId, Guid productId, int quantity, CancellationToken cancellationToken);
    Task RemoveAsync(Guid userId, Guid productId, CancellationToken cancellationToken);
    Task ClearAsync(Guid userId, CancellationToken cancellationToken);
}
