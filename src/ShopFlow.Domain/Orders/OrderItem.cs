using ShopFlow.Domain.Common;

namespace ShopFlow.Domain.Orders;

/// <summary>
/// Immutable snapshot of a product at the moment the order was placed.
/// Later price or name changes on the product do not affect existing orders.
/// </summary>
public sealed class OrderItem : Entity, IAuditable
{
    private OrderItem() { } // EF Core

    internal OrderItem(Guid productId, string sku, string productName, decimal unitPrice, int quantity)
    {
        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
