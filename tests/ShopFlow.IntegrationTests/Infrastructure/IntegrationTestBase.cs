using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopFlow.Application.Auth;
using ShopFlow.Application.Cart;
using ShopFlow.Application.Products;
using ShopFlow.Infrastructure.Persistence;

namespace ShopFlow.IntegrationTests.Infrastructure;

[Collection(ApiCollection.Name)]
public abstract class IntegrationTestBase(ShopFlowApiFactory factory)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ShopFlowApiFactory Factory { get; } = factory;

    protected HttpClient Anonymous() => Factory.CreateClient();

    protected async Task<HttpClient> AdminAsync()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ShopFlowApiFactory.AdminEmail, ShopFlowApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    /// <summary>Registers a brand-new customer so every test works with its own isolated data.</summary>
    protected async Task<(HttpClient Client, Guid UserId)> NewCustomerAsync()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"customer-{Guid.NewGuid():N}@shopflow.test", "Passw0rd!", "Test Customer"));
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.UserId);
    }

    protected async Task<ProductDto> CreateProductAsync(int stock = 10, decimal price = 25m)
    {
        var admin = await AdminAsync();
        var sku = $"T-{Guid.NewGuid():N}"[..20];
        var response = await admin.PostAsJsonAsync("/api/products",
            new CreateProductRequest(sku, $"Product {sku}", "integration test product", price, stock));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>(Json))!;
    }

    protected static async Task AddToCartAsync(HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest(productId, quantity));
        response.EnsureSuccessStatusCode();
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<T>(Json);
        body.Should().NotBeNull();
        return body!;
    }

    /// <summary>Runs code against the database directly, bypassing the API (for assertions).</summary>
    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    protected Task<int> StockOfAsync(Guid productId) =>
        WithDbAsync(db => db.Products.Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync());
}
