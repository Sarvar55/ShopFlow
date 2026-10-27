using Microsoft.EntityFrameworkCore;
using ShopFlow.Domain.Auditing;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Payments;
using ShopFlow.Domain.Products;
using ShopFlow.Domain.Users;

namespace ShopFlow.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Order> Orders { get; }
    DbSet<Payment> Payments { get; }
    DbSet<User> Users { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets the version the client last saw, so a stale update fails with a concurrency conflict.</summary>
    void SetExpectedVersion(Product product, uint expectedVersion);
}
