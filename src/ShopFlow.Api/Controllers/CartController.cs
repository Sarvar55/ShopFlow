using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopFlow.Application.Abstractions;
using ShopFlow.Application.Cart;
using ShopFlow.Application.Common;

namespace ShopFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public sealed class CartController(CartService cart, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<CartDto> Get(CancellationToken ct) => cart.GetAsync(currentUser.RequireUserId(), ct);

    [HttpPost("items")]
    public Task<CartDto> AddItem(AddCartItemRequest request, CancellationToken ct) =>
        cart.AddItemAsync(currentUser.RequireUserId(), request, ct);

    [HttpPut("items/{productId:guid}")]
    public Task<CartDto> UpdateItem(Guid productId, UpdateCartItemRequest request, CancellationToken ct) =>
        cart.UpdateItemAsync(currentUser.RequireUserId(), productId, request, ct);

    [HttpDelete("items/{productId:guid}")]
    public Task<CartDto> RemoveItem(Guid productId, CancellationToken ct) =>
        cart.RemoveItemAsync(currentUser.RequireUserId(), productId, ct);

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await cart.ClearAsync(currentUser.RequireUserId(), ct);
        return NoContent();
    }
}
