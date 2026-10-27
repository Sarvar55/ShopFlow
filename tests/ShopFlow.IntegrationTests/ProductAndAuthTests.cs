using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ShopFlow.Application.Auth;
using ShopFlow.Application.Products;
using ShopFlow.IntegrationTests.Infrastructure;

namespace ShopFlow.IntegrationTests;

public class ProductAndAuthTests(ShopFlowApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Stale_update_is_rejected_with_409_optimistic_concurrency()
    {
        var product = await CreateProductAsync();
        var admin = await AdminAsync();

        var first = await admin.PutAsJsonAsync($"/api/products/{product.Id}",
            new UpdateProductRequest("Renamed once", null, 30m, product.Version));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var stale = await admin.PutAsJsonAsync($"/api/products/{product.Id}",
            new UpdateProductRequest("Renamed twice", null, 31m, product.Version));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cached_product_is_invalidated_after_update()
    {
        var product = await CreateProductAsync(price: 10m);
        var anonymous = Anonymous();
        (await ReadAsync<ProductDto>(await anonymous.GetAsync($"/api/products/{product.Id}"))).Price.Should().Be(10m);

        var admin = await AdminAsync();
        (await admin.PutAsJsonAsync($"/api/products/{product.Id}",
            new UpdateProductRequest(product.Name, null, 12.5m, product.Version))).EnsureSuccessStatusCode();

        (await ReadAsync<ProductDto>(await anonymous.GetAsync($"/api/products/{product.Id}"))).Price.Should().Be(12.5m);
    }

    [Fact]
    public async Task Customers_cannot_create_products()
    {
        var (customer, _) = await NewCustomerAsync();

        var response = await customer.PostAsJsonAsync("/api/products", new CreateProductRequest("X-1", "X", null, 1m, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_users_cannot_use_the_cart()
    {
        (await Anonymous().GetAsync("/api/cart")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registering_twice_with_same_email_is_a_conflict()
    {
        var request = new RegisterRequest($"dup-{Guid.NewGuid():N}@shopflow.test", "Passw0rd!", "Dup");

        (await Anonymous().PostAsJsonAsync("/api/auth/register", request)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Anonymous().PostAsJsonAsync("/api/auth/register", request with { Email = request.Email.ToUpperInvariant() }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ShopFlowApiFactory.AdminEmail, "wrong-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_request_returns_validation_problem()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/register", new RegisterRequest("not-an-email", "123", ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("errors");
    }

    [Fact]
    public async Task Readiness_probe_checks_postgres_and_redis()
    {
        (await Anonymous().GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
