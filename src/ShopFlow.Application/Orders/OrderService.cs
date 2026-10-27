using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.Application.Abstractions;
using ShopFlow.Application.Common;
using ShopFlow.Application.Products;
using ShopFlow.Domain.Common;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Payments;

namespace ShopFlow.Application.Orders;

public sealed class OrderService(
    IAppDbContext db,
    IUnitOfWork unitOfWork,
    IRowLocker locker,
    ICartStore cartStore,
    ProductService productService,
    IPaymentGateway paymentGateway,
    ICurrentUser currentUser,
    TimeProvider clock,
    ILogger<OrderService> logger)
{
    public const int MaxIdempotencyKeyLength = 100;

    /// <summary>
    /// Turns the current cart into an order.
    /// Transaction boundary: lock products -> validate and reserve stock -> insert order + items
    /// (+ audit rows) -> commit. Redis side effects (clear cart, invalidate cache) happen only after
    /// the commit succeeded, so a rollback never leaves the cart emptied for an order that does not exist.
    /// </summary>
    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, string? idempotencyKey, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (idempotencyKey?.Length > MaxIdempotencyKeyLength)
            throw new ValidationException($"Idempotency-Key must be at most {MaxIdempotencyKeyLength} characters.");

        if (idempotencyKey is not null && await FindByIdempotencyKeyAsync(userId, idempotencyKey, ct) is { } replay)
        {
            logger.LogInformation("Checkout replayed for idempotency key, returning order {OrderId}", replay.Id);
            return new CheckoutResult(replay, Created: false);
        }

        var cart = await cartStore.GetAsync(userId, ct);
        if (cart.Count == 0)
        {
            // A concurrent retry with the same key may have completed and cleared the cart in the meantime.
            if (idempotencyKey is not null && await FindByIdempotencyKeyAsync(userId, idempotencyKey, ct) is { } completed)
                return new CheckoutResult(completed, Created: false);
            throw new ValidationException("Cart is empty.");
        }

        Order order;
        try
        {
            order = await unitOfWork.ExecuteInTransactionAsync(async txCt =>
            {
                var locked = await locker.LockProductsAsync(cart.Keys.ToList(), txCt);
                var products = locked.ToDictionary(p => p.Id);

                var missing = cart.Keys.Where(id => !products.ContainsKey(id)).ToList();
                if (missing.Count > 0)
                    throw new ValidationException($"Cart contains products that no longer exist: {string.Join(", ", missing)}.");

                var placed = Order.Place(
                    userId,
                    request.ShippingAddress,
                    cart.Select(line => (products[line.Key], line.Value)).ToList(),
                    idempotencyKey);

                db.Orders.Add(placed);
                await db.SaveChangesAsync(txCt);
                return placed;
            }, cancellationToken: ct);
        }
        catch (UniqueConstraintException) when (idempotencyKey is not null)
        {
            // Two requests with the same key raced; the other one won. Return its order.
            var existing = await FindByIdempotencyKeyAsync(userId, idempotencyKey, ct)
                ?? throw new ConflictException("A concurrent checkout with the same Idempotency-Key is in progress.");
            return new CheckoutResult(existing, Created: false);
        }

        logger.LogInformation(
            "Order {OrderId} placed by {UserId} with {ItemCount} item(s), total {TotalAmount}",
            order.Id, userId, order.Items.Count, order.TotalAmount);

        await cartStore.ClearAsync(userId, ct);
        await productService.InvalidateAsync(order.Items.Select(i => i.ProductId), ct);

        return new CheckoutResult(order.ToDto(), Created: true);
    }

    /// <summary>
    /// Charges the order. A declined payment is still committed (as a Declined payment row plus its
    /// audit entry) so failed attempts remain traceable; the order simply stays Pending.
    /// </summary>
    public async Task<PaymentDto> PayAsync(Guid orderId, PayOrderRequest request, CancellationToken ct)
    {
        var payment = await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            // Row lock prevents two concurrent requests from paying the same order twice.
            var order = await locker.LockOrderAsync(orderId, txCt);
            EnsureCanAccess(order, orderId);

            if (order!.Status != OrderStatus.Pending)
                throw new InvalidOrderStateException(order.Id, order.Status.ToString(), "paid");

            var charge = await paymentGateway.ChargeAsync(order.Id, order.TotalAmount, request.PaymentToken, txCt);
            var now = clock.GetUtcNow();

            Payment result;
            if (charge.Succeeded)
            {
                result = Payment.Succeeded(order.Id, order.TotalAmount, charge.ProviderReference!, now);
                order.MarkPaid();
            }
            else
            {
                result = Payment.Declined(order.Id, order.TotalAmount, charge.FailureReason ?? "Declined", now);
            }

            db.Payments.Add(result);
            await db.SaveChangesAsync(txCt);
            return result;
        }, cancellationToken: ct);

        if (payment.Status == PaymentStatus.Succeeded)
            logger.LogInformation("Order {OrderId} paid, amount {Amount}, reference {ProviderReference}", orderId, payment.Amount, payment.ProviderReference);
        else
            logger.LogWarning("Payment for order {OrderId} declined: {FailureReason}", orderId, payment.FailureReason);

        return payment.ToDto();
    }

    /// <summary>Cancels the order and returns its items to stock in a single transaction.</summary>
    public async Task<OrderDto> CancelAsync(Guid orderId, CancellationToken ct)
    {
        var order = await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var order = await locker.LockOrderAsync(orderId, txCt);
            EnsureCanAccess(order, orderId);

            var products = await locker.LockProductsAsync(order!.Items.Select(i => i.ProductId).ToList(), txCt);
            order.Cancel(products.ToDictionary(p => p.Id));

            await db.SaveChangesAsync(txCt);
            return order;
        }, cancellationToken: ct);

        logger.LogInformation("Order {OrderId} cancelled, stock released", order.Id);
        await productService.InvalidateAsync(order.Items.Select(i => i.ProductId), ct);
        return order.ToDto();
    }

    public Task<OrderDto> ShipAsync(Guid orderId, CancellationToken ct) => TransitionAsync(orderId, o => o.Ship(), ct);

    public Task<OrderDto> DeliverAsync(Guid orderId, CancellationToken ct) => TransitionAsync(orderId, o => o.Deliver(), ct);

    public async Task<OrderDto> GetAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        EnsureCanAccess(order, orderId);
        return order!.ToDto();
    }

    public async Task<PagedResult<OrderDto>> ListAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var userId = currentUser.RequireUserId();

        var query = db.Orders.AsNoTracking();
        if (!currentUser.IsAdmin)
            query = query.Where(o => o.CustomerId == userId);
        if (status is not null)
            query = query.Where(o => o.Status == status);

        var total = await query.CountAsync(ct);
        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(orders.Select(o => o.ToDto()).ToList(), page, pageSize, total);
    }

    private async Task<OrderDto> TransitionAsync(Guid orderId, Action<Order> transition, CancellationToken ct)
    {
        var order = await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var order = await locker.LockOrderAsync(orderId, txCt) ?? throw new NotFoundException("Order", orderId);
            transition(order);
            await db.SaveChangesAsync(txCt);
            return order;
        }, cancellationToken: ct);

        logger.LogInformation("Order {OrderId} moved to {Status}", order.Id, order.Status);
        return order.ToDto();
    }

    private async Task<OrderDto?> FindByIdempotencyKeyAsync(Guid userId, string key, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.CustomerId == userId && o.IdempotencyKey == key, ct);
        return order?.ToDto();
    }

    // Customers only see their own orders. Someone else's order is reported as "not found"
    // rather than "forbidden" so order ids cannot be probed.
    private void EnsureCanAccess(Order? order, Guid orderId)
    {
        if (order is null || (!currentUser.IsAdmin && order.CustomerId != currentUser.RequireUserId()))
            throw new NotFoundException("Order", orderId);
    }
}
