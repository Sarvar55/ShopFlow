using ShopFlow.Domain.Common;
using ShopFlow.Domain.Products;

namespace ShopFlow.Domain.Orders;

public sealed class Order : Entity, IAuditable, IHasTimestamps
{
    public const int MaxAddressLength = 500;

    private readonly List<OrderItem> _items = [];

    private Order() { } // EF Core

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string ShippingAddress { get; private set; } = null!;
    public string? IdempotencyKey { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Places an order and reserves stock for every line. All lines are validated before any
    /// stock is touched, so a failing line never leaves the other products partially reserved.
    /// </summary>
    public static Order Place(
        Guid customerId,
        string shippingAddress,
        IReadOnlyCollection<(Product Product, int Quantity)> lines,
        string? idempotencyKey = null)
    {
        if (customerId == Guid.Empty)
            throw new DomainException("Customer is required.");
        if (string.IsNullOrWhiteSpace(shippingAddress) || shippingAddress.Length > MaxAddressLength)
            throw new DomainException($"Shipping address is required and must be at most {MaxAddressLength} characters.");
        if (lines.Count == 0)
            throw new DomainException("An order must contain at least one item.");
        if (lines.Select(l => l.Product.Id).Distinct().Count() != lines.Count)
            throw new DomainException("Each product may appear only once per order.");

        foreach (var (product, quantity) in lines)
            product.EnsureCanReserve(quantity);

        var order = new Order
        {
            CustomerId = customerId,
            ShippingAddress = shippingAddress.Trim(),
            IdempotencyKey = idempotencyKey,
            Status = OrderStatus.Pending
        };

        foreach (var (product, quantity) in lines)
        {
            product.ReserveStock(quantity);
            order._items.Add(new OrderItem(product.Id, product.Sku, product.Name, product.Price, quantity));
        }

        order.TotalAmount = order._items.Sum(i => i.LineTotal);
        return order;
    }

    public void MarkPaid()
    {
        EnsureStatus(OrderStatus.Pending, "paid");
        Status = OrderStatus.Paid;
    }

    public void Ship()
    {
        EnsureStatus(OrderStatus.Paid, "shipped");
        Status = OrderStatus.Shipped;
    }

    public void Deliver()
    {
        EnsureStatus(OrderStatus.Shipped, "delivered");
        Status = OrderStatus.Delivered;
    }

    /// <summary>
    /// Cancels the order and returns the reserved quantities to stock.
    /// The caller must pass the (locked) products referenced by the order items.
    /// </summary>
    public void Cancel(IReadOnlyDictionary<Guid, Product> products)
    {
        if (Status is not (OrderStatus.Pending or OrderStatus.Paid))
            throw new InvalidOrderStateException(Id, Status.ToString(), "cancelled");

        foreach (var item in _items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new DomainException($"Product {item.ProductId} is required to cancel order {Id}.");
            product.ReleaseStock(item.Quantity);
        }

        Status = OrderStatus.Cancelled;
    }

    private void EnsureStatus(OrderStatus expected, string action)
    {
        if (Status != expected)
            throw new InvalidOrderStateException(Id, Status.ToString(), action);
    }
}
