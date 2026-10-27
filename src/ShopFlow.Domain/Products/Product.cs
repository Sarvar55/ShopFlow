using ShopFlow.Domain.Common;

namespace ShopFlow.Domain.Products;

public sealed class Product : Entity, IAuditable, IHasTimestamps
{
    public const int MaxNameLength = 200;
    public const int MaxSkuLength = 64;

    private Product() { } // EF Core

    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Concurrency token, mapped to PostgreSQL's xmin system column.</summary>
    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Product Create(string sku, string name, string? description, decimal price, int initialStock)
    {
        if (string.IsNullOrWhiteSpace(sku) || sku.Length > MaxSkuLength)
            throw new DomainException($"SKU is required and must be at most {MaxSkuLength} characters.");
        if (initialStock < 0)
            throw new DomainException("Initial stock cannot be negative.");

        var product = new Product
        {
            Sku = sku.Trim().ToUpperInvariant(),
            StockQuantity = initialStock,
            IsActive = true
        };
        product.UpdateDetails(name, description, price);
        return product;
    }

    public void UpdateDetails(string name, string? description, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
            throw new DomainException($"Name is required and must be at most {MaxNameLength} characters.");
        if (price <= 0)
            throw new DomainException("Price must be greater than zero.");
        if (decimal.Round(price, 2) != price)
            throw new DomainException("Price cannot have more than two decimal places.");

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Price = price;
    }

    public void EnsureCanReserve(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");
        if (!IsActive)
            throw new DomainException($"Product '{Sku}' is no longer available.");
        if (quantity > StockQuantity)
            throw new InsufficientStockException(Sku, quantity, StockQuantity);
    }

    public void ReserveStock(int quantity)
    {
        EnsureCanReserve(quantity);
        StockQuantity -= quantity;
    }

    public void ReleaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");
        StockQuantity += quantity;
    }

    public void Restock(int quantity) => ReleaseStock(quantity);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
