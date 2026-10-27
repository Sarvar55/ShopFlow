using Microsoft.EntityFrameworkCore;
using ShopFlow.Application.Abstractions;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Products;

namespace ShopFlow.Infrastructure.Persistence;

public sealed class RowLocker(AppDbContext db) : IRowLocker
{
    public async Task<IReadOnlyList<Product>> LockProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var ids = productIds.Distinct().ToArray();

        // xmin is a system column and is not part of "*", but EF needs it for the row version.
        return await db.Products
            .FromSql($"SELECT p.*, p.xmin FROM products p WHERE p.id = ANY({ids}) ORDER BY p.id FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public async Task<Order?> LockOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        EnsureTransaction();

        var order = await db.Orders
            .FromSql($"SELECT o.*, o.xmin FROM orders o WHERE o.id = {orderId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (order is not null)
            await db.Entry(order).Collection(o => o.Items).LoadAsync(cancellationToken);

        return order;
    }

    private void EnsureTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Row locks require an active transaction (use IUnitOfWork).");
    }
}
