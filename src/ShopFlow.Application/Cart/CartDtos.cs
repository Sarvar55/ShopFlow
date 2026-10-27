using System.ComponentModel.DataAnnotations;

namespace ShopFlow.Application.Cart;

public sealed record CartItemDto(Guid ProductId, string Sku, string Name, decimal UnitPrice, int Quantity, decimal LineTotal, bool Available);

public sealed record CartDto(IReadOnlyList<CartItemDto> Items, decimal Total);

public sealed record AddCartItemRequest(Guid ProductId, [Range(1, 100)] int Quantity);

public sealed record UpdateCartItemRequest([Range(1, 100)] int Quantity);
