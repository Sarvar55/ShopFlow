using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Products;

namespace ShopFlow.Application.Abstractions;

/// <summary>
/// Pessimistic row locks (SELECT ... FOR UPDATE). Must be called inside a transaction;
/// the locks are held until that transaction commits or rolls back.
/// </summary>
public interface IRowLocker
{
    /// <summary>Locks products in a deterministic order (by id) to avoid deadlocks between concurrent checkouts.</summary>
    Task<IReadOnlyList<Product>> LockProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task<Order?> LockOrderAsync(Guid orderId, CancellationToken cancellationToken);
}
