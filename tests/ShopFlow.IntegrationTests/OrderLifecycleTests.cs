using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShopFlow.Application.Orders;
using ShopFlow.Domain.Auditing;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Payments;
using ShopFlow.IntegrationTests.Infrastructure;

namespace ShopFlow.IntegrationTests;

public class OrderLifecycleTests(ShopFlowApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(HttpClient Customer, OrderDto Order, Guid ProductId)> PlaceOrderAsync(int quantity = 2, int stock = 10)
    {
        var product = await CreateProductAsync(stock: stock, price: 40m);
        var (customer, _) = await NewCustomerAsync();
        await AddToCartAsync(customer, product.Id, quantity);
        var response = await customer.PostAsJsonAsync("/api/orders/checkout", new CheckoutRequest("Baku, Nizami st. 10"));
        response.EnsureSuccessStatusCode();
        return (customer, await ReadAsync<OrderDto>(response), product.Id);
    }

    [Fact]
    public async Task Declined_payment_is_recorded_and_order_stays_pending_then_succeeds_on_retry()
    {
        var (customer, order, _) = await PlaceOrderAsync();

        var declined = await customer.PostAsJsonAsync($"/api/orders/{order.Id}/pay", new PayOrderRequest("tok_declined"));
        declined.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await ReadAsync<OrderDto>(await customer.GetAsync($"/api/orders/{order.Id}"))).Status.Should().Be(OrderStatus.Pending);

        var paid = await customer.PostAsJsonAsync($"/api/orders/{order.Id}/pay", new PayOrderRequest("tok_visa"));
        paid.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<PaymentDto>(paid)).Status.Should().Be(PaymentStatus.Succeeded);
        (await ReadAsync<OrderDto>(await customer.GetAsync($"/api/orders/{order.Id}"))).Status.Should().Be(OrderStatus.Paid);

        var payments = await WithDbAsync(db => db.Payments.Where(p => p.OrderId == order.Id).ToListAsync());
        payments.Select(p => p.Status).Should().BeEquivalentTo([PaymentStatus.Declined, PaymentStatus.Succeeded]);
    }

    [Fact]
    public async Task Paying_twice_is_rejected()
    {
        var (customer, order, _) = await PlaceOrderAsync();
        (await customer.PostAsJsonAsync($"/api/orders/{order.Id}/pay", new PayOrderRequest("tok_visa"))).EnsureSuccessStatusCode();

        var again = await customer.PostAsJsonAsync($"/api/orders/{order.Id}/pay", new PayOrderRequest("tok_visa"));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cancel_returns_stock_and_is_audited()
    {
        var (customer, order, productId) = await PlaceOrderAsync(quantity: 3, stock: 5);
        (await StockOfAsync(productId)).Should().Be(2);

        var response = await customer.PostAsync($"/api/orders/{order.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<OrderDto>(response)).Status.Should().Be(OrderStatus.Cancelled);
        (await StockOfAsync(productId)).Should().Be(5);

        var orderAudit = await WithDbAsync(db => db.AuditLogs
            .Where(a => a.EntityType == "Order" && a.EntityId == order.Id.ToString() && a.Action == AuditAction.Update)
            .ToListAsync());
        orderAudit.Should().ContainSingle().Which.Changes.Should().Contain("Cancelled");
    }

    [Fact]
    public async Task Admin_ships_and_delivers_paid_order()
    {
        var (customer, order, _) = await PlaceOrderAsync();
        var admin = await AdminAsync();

        (await admin.PostAsync($"/api/orders/{order.Id}/ship", null)).StatusCode.Should().Be(HttpStatusCode.Conflict, "order is not paid yet");

        (await customer.PostAsJsonAsync($"/api/orders/{order.Id}/pay", new PayOrderRequest("tok_visa"))).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/orders/{order.Id}/ship", null)).EnsureSuccessStatusCode();
        var delivered = await admin.PostAsync($"/api/orders/{order.Id}/deliver", null);

        (await ReadAsync<OrderDto>(delivered)).Status.Should().Be(OrderStatus.Delivered);
        (await customer.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customers_cannot_see_or_ship_other_customers_orders()
    {
        var (_, order, _) = await PlaceOrderAsync();
        var (stranger, _) = await NewCustomerAsync();

        (await stranger.GetAsync($"/api/orders/{order.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/orders/{order.Id}/ship", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
