using Microsoft.EntityFrameworkCore;
using ShopFlow.Application.Abstractions;
using ShopFlow.Domain.Auditing;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Payments;
using ShopFlow.Domain.Products;
using ShopFlow.Domain.Users;

namespace ShopFlow.UnitTests.Fakes;

/// <summary>In-memory EF context for fast service tests. Transactions and SQL locks are covered by integration tests.</summary>
public sealed class TestDbContext : DbContext, IAppDbContext
{
    public TestDbContext() : base(new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public void SetExpectedVersion(Product product, uint expectedVersion) =>
        Entry(product).Property(p => p.Version).OriginalValue = expectedVersion;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        modelBuilder.Entity<OrderItem>().Ignore(i => i.LineTotal);
    }
}
