using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShopFlow.Application.Cart;
using ShopFlow.Application.Orders;
using ShopFlow.Domain.Auditing;
using ShopFlow.Domain.Orders;
using ShopFlow.IntegrationTests.Infrastructure;

namespace ShopFlow.IntegrationTests;

public class CheckoutTests(ShopFlowApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly CheckoutRequest Checkout = new("Baku, Akademik Hasan Aliyev st. 24");

    [Fact]
    public async Task Checkout_creates_order_reserves_stock_clears_cart_and_writes_audit_in_one_transaction()
    {
        var product = await CreateProductAsync(stock: 10, price: 19.99m);
        var (customer, userId) = await NewCustomerAsync();
        await AddToCartAsync(customer, product.Id, 3);
        var correlationId = $"test-{Guid.NewGuid():N}";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/checkout") { Content = JsonContent.Create(Checkout) };
        request.Headers.Add("X-Correlation-Id", correlationId);
        var response = await customer.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be(correlationId);
        var order = await ReadAsync<OrderDto>(response);
        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(59.97m);

        (await StockOfAsync(product.Id)).Should().Be(7);
        (await ReadAsync<CartDto>(await customer.GetAsync("/api/cart"))).Items.Should().BeEmpty();

        var audit = await WithDbAsync(db => db.AuditLogs.Where(a => a.CorrelationId == correlationId).ToListAsync());
        audit.Should().Contain(a => a.EntityType == "Order" && a.Action == AuditAction.Insert && a.EntityId == order.Id.ToString());
        audit.Should().Contain(a => a.EntityType == "OrderItem" && a.Action == AuditAction.Insert);
        audit.Should().Contain(a => a.EntityType == "Product" && a.Action == AuditAction.Update && a.Changes.Contains("StockQuantity"));
        audit.Should().OnlyContain(a => a.UserId == userId);
    }

    [Fact]
    public async Task Checkout_with_insufficient_stock_rolls_back_everything()
    {
        var plentiful = await CreateProductAsync(stock: 10);
        var scarce = await CreateProductAsync(stock: 2);
        var (first, _) = await NewCustomerAsync();
        var (second, secondUserId) = await NewCustomerAsync();

        // Both customers put the last two units in their cart; the first one buys them.
        await AddToCartAsync(first, scarce.Id, 2);
        await AddToCartAsync(second, plentiful.Id, 4);
        await AddToCartAsync(second, scarce.Id, 2);
        (await first.PostAsJsonAsync("/api/orders/checkout", Checkout)).StatusCode.Should().Be(HttpStatusCode.Created);

        var correlationId = $"test-{Guid.NewGuid():N}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/checkout") { Content = JsonContent.Create(Checkout) };
        request.Headers.Add("X-Correlation-Id", correlationId);
        var response = await second.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StockOfAsync(plentiful.Id)).Should().Be(10, "the reservation of the first line must be rolled back");
        (await StockOfAsync(scarce.Id)).Should().Be(0);
        (await WithDbAsync(db => db.Orders.CountAsync(o => o.CustomerId == secondUserId))).Should().Be(0);
        (await WithDbAsync(db => db.AuditLogs.CountAsync(a => a.CorrelationId == correlationId)))
            .Should().Be(0, "audit rows share the business transaction and are rolled back with it");
        (await ReadAsync<CartDto>(await second.GetAsync("/api/cart"))).Items.Should().HaveCount(2, "the cart is kept so the customer can adjust it");
    }

    [Fact]
    public async Task Concurrent_checkouts_for_the_last_unit_never_oversell()
    {
        const int buyers = 6;
        var product = await CreateProductAsync(stock: 1);
        var customers = new List<HttpClient>();
        for (var i = 0; i < buyers; i++)
        {
            var (client, _) = await NewCustomerAsync();
            await AddToCartAsync(client, product.Id, 1);
            customers.Add(client);
        }

        var responses = await Task.WhenAll(customers.Select(c => c.PostAsJsonAsync("/api/orders/checkout", Checkout)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(buyers - 1);
        (await StockOfAsync(product.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Retried_checkout_with_same_idempotency_key_returns_the_original_order()
    {
        var product = await CreateProductAsync(stock: 10);
        var (customer, userId) = await NewCustomerAsync();
        await AddToCartAsync(customer, product.Id, 2);
        var key = Guid.NewGuid().ToString();

        async Task<HttpResponseMessage> CheckoutWithKey()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/checkout") { Content = JsonContent.Create(Checkout) };
            request.Headers.Add("Idempotency-Key", key);
            return await customer.SendAsync(request);
        }

        var responses = await Task.WhenAll(CheckoutWithKey(), CheckoutWithKey(), CheckoutWithKey());

        responses.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.Created || s == HttpStatusCode.OK);
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        var orderIds = await Task.WhenAll(responses.Select(async r => (await ReadAsync<OrderDto>(r)).Id));
        orderIds.Distinct().Should().ContainSingle();
        (await StockOfAsync(product.Id)).Should().Be(8, "stock must be reserved exactly once");
        (await WithDbAsync(db => db.Orders.CountAsync(o => o.CustomerId == userId))).Should().Be(1);
    }

    [Fact]
    public async Task Checkout_with_empty_cart_is_bad_request()
    {
        var (customer, _) = await NewCustomerAsync();

        var response = await customer.PostAsJsonAsync("/api/orders/checkout", Checkout);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
