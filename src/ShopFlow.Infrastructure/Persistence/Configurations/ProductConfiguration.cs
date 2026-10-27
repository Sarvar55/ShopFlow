using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopFlow.Domain.Products;

namespace ShopFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", t =>
        {
            // Last line of defence: even a buggy code path cannot oversell.
            t.HasCheckConstraint("ck_products_stock_non_negative", "stock_quantity >= 0");
            t.HasCheckConstraint("ck_products_price_positive", "price > 0");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.Sku).HasMaxLength(Product.MaxSkuLength).IsRequired();
        b.HasIndex(p => p.Sku).IsUnique();
        b.Property(p => p.Name).HasMaxLength(Product.MaxNameLength).IsRequired();
        b.Property(p => p.Description).HasMaxLength(2000);
        b.Property(p => p.Price).HasPrecision(12, 2);
        b.Property(p => p.Version).IsRowVersion();
    }
}
