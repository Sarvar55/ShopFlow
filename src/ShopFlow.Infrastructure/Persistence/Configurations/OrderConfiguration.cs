using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Users;

namespace ShopFlow.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders", t => t.HasCheckConstraint("ck_orders_total_non_negative", "total_amount >= 0"));
        b.HasKey(o => o.Id);
        b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(o => o.TotalAmount).HasPrecision(12, 2);
        b.Property(o => o.ShippingAddress).HasMaxLength(Order.MaxAddressLength).IsRequired();
        b.Property(o => o.IdempotencyKey).HasMaxLength(100);
        b.Property(o => o.Version).IsRowVersion();

        b.HasOne<User>().WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(o => new { o.CustomerId, o.CreatedAt });

        // Guarantees at most one order per (customer, Idempotency-Key), even under concurrent retries.
        b.HasIndex(o => new { o.CustomerId, o.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ux_orders_customer_idempotency_key");

        b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("order_items", t => t.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0"));
        b.HasKey(i => i.Id);
        b.Property(i => i.Sku).HasMaxLength(64).IsRequired();
        b.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        b.Property(i => i.UnitPrice).HasPrecision(12, 2);
        b.Ignore(i => i.LineTotal);
        b.HasOne<ShopFlow.Domain.Products.Product>().WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
