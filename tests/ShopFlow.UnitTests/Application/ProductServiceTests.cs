using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ShopFlow.Application.Common;
using ShopFlow.Application.Products;
using ShopFlow.UnitTests.Fakes;

namespace ShopFlow.UnitTests.Application;

public class ProductServiceTests
{
    private readonly TestDbContext _db = new();
    private readonly InMemoryCacheService _cache = new();
    private readonly ProductService _sut;

    public ProductServiceTests() => _sut = new ProductService(_db, _cache, NullLogger<ProductService>.Instance);

    [Fact]
    public async Task GetById_is_served_from_cache_on_second_call()
    {
        var created = await _sut.CreateAsync(new CreateProductRequest("SKU-1", "Phone", null, 499m, 3), default);

        await _sut.GetByIdAsync(created.Id, default);
        await _sut.GetByIdAsync(created.Id, default);

        _cache.Hits.Should().Be(1);
    }

    [Fact]
    public async Task Restock_invalidates_cached_product()
    {
        var created = await _sut.CreateAsync(new CreateProductRequest("SKU-2", "Phone", null, 499m, 3), default);
        await _sut.GetByIdAsync(created.Id, default);

        await _sut.RestockAsync(created.Id, new RestockRequest(7), default);

        _cache.Contains(ProductService.ProductKey(created.Id)).Should().BeFalse();
        (await _sut.GetByIdAsync(created.Id, default)).StockQuantity.Should().Be(10);
    }

    [Fact]
    public async Task Create_with_duplicate_sku_is_a_conflict()
    {
        await _sut.CreateAsync(new CreateProductRequest("dup", "A", null, 1m, 1), default);

        var act = () => _sut.CreateAsync(new CreateProductRequest("DUP", "B", null, 1m, 1), default);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task List_hides_inactive_products_and_pages_results()
    {
        for (var i = 0; i < 5; i++)
            await _sut.CreateAsync(new CreateProductRequest($"P{i}", $"Product {i}", null, 1m, 1), default);
        var hidden = await _sut.CreateAsync(new CreateProductRequest("HIDDEN", "Hidden", null, 1m, 1), default);
        await _sut.DeactivateAsync(hidden.Id, default);

        var page = await _sut.ListAsync(null, page: 2, pageSize: 2, includeInactive: false, default);

        page.TotalCount.Should().Be(5);
        page.TotalPages.Should().Be(3);
        page.Items.Select(p => p.Name).Should().Equal("Product 2", "Product 3");
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(3, 500, 3, 100)]
    public void Paging_is_normalized(int page, int size, int expectedPage, int expectedSize)
    {
        Paging.Normalize(page, size).Should().Be((expectedPage, expectedSize));
    }
}
