using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopFlow.Application.Common;
using ShopFlow.Application.Orders;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Payments;

namespace ShopFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    /// <summary>
    /// Creates an order from the cart. Send an Idempotency-Key header to make retries safe:
    /// repeating the request with the same key returns the original order (200) instead of a new one (201).
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(
        CheckoutRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var result = await orders.CheckoutAsync(request, idempotencyKey, ct);
        return result.Created
            ? CreatedAtAction(nameof(Get), new { id = result.Order.Id }, result.Order)
            : Ok(result.Order);
    }

    /// <summary>Customers see their own orders; admins see all orders.</summary>
    [HttpGet]
    public Task<PagedResult<OrderDto>> List(
        [FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        orders.ListAsync(status, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) => orders.GetAsync(id, ct);

    /// <summary>Pays a pending order. Use PaymentToken "tok_declined" to simulate a declined card (402).</summary>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    public async Task<IActionResult> Pay(Guid id, PayOrderRequest request, CancellationToken ct)
    {
        var payment = await orders.PayAsync(id, request, ct);
        if (payment.Status == PaymentStatus.Succeeded)
            return Ok(payment);

        return Problem(
            statusCode: StatusCodes.Status402PaymentRequired,
            title: "Payment declined",
            detail: payment.FailureReason);
    }

    [HttpPost("{id:guid}/cancel")]
    public Task<OrderDto> Cancel(Guid id, CancellationToken ct) => orders.CancelAsync(id, ct);

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/ship")]
    public Task<OrderDto> Ship(Guid id, CancellationToken ct) => orders.ShipAsync(id, ct);

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/deliver")]
    public Task<OrderDto> Deliver(Guid id, CancellationToken ct) => orders.DeliverAsync(id, ct);
}
