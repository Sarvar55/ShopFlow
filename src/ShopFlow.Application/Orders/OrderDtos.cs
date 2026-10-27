using System.ComponentModel.DataAnnotations;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Payments;

namespace ShopFlow.Application.Orders;

public sealed record OrderItemDto(Guid ProductId, string Sku, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(
    Guid Id, Guid CustomerId, OrderStatus Status, decimal TotalAmount, string ShippingAddress,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<OrderItemDto> Items);

public sealed record CheckoutRequest([Required, StringLength(Order.MaxAddressLength, MinimumLength = 5)] string ShippingAddress);

public sealed record CheckoutResult(OrderDto Order, bool Created);

/// <summary>
/// Simulated card token. Use "tok_declined" to simulate a declined card; anything else succeeds.
/// </summary>
public sealed record PayOrderRequest([Required, StringLength(100)] string PaymentToken);

public sealed record PaymentDto(Guid Id, Guid OrderId, decimal Amount, PaymentStatus Status, string? ProviderReference, string? FailureReason, DateTimeOffset CreatedAt);

internal static class OrderMappings
{
    public static OrderDto ToDto(this Order o) => new(
        o.Id, o.CustomerId, o.Status, o.TotalAmount, o.ShippingAddress, o.CreatedAt, o.UpdatedAt,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.Sku, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal)).ToList());

    public static PaymentDto ToDto(this Payment p) =>
        new(p.Id, p.OrderId, p.Amount, p.Status, p.ProviderReference, p.FailureReason, p.CreatedAt);
}
