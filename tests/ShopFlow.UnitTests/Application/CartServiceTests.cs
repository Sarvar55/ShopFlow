using FluentAssertions;
using ShopFlow.Application.Cart;
using ShopFlow.Application.Common;
using ShopFlow.Domain.Products;
using ShopFlow.UnitTests.Fakes;

namespace ShopFlow.UnitTests.Application;

public class CartServiceTests
{
    private readonly TestDbContext _db = new();
    private readonly InMemoryCartStore _store = new();
    private readonly CartService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public CartServiceTests() => _sut = new CartService(_db, _store);

    private async Task<Product> AddProductAsync(decimal price = 10m, int stock = 5)
    {
        var product = Product.Create($"SKU-{Guid.NewGuid():N}"[..20], "Test product", null, price, stock);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task AddItem_accumulates_quantity_and_calculates_total()
    {
        var product = await AddProductAsync(price: 12.5m);

        await _sut.AddItemAsync(_userId, new AddCartItemRequest(product.Id, 1), default);
        var cart = await _sut.AddItemAsync(_userId, new AddCartItemRequest(product.Id, 2), default);

        cart.Items.Should().ContainSingle().Which.Quantity.Should().Be(3);
        cart.Total.Should().Be(37.5m);
    }

    [Fact]
    public async Task AddItem_more_than_stock_is_rejected()
    {
        var product = await AddProductAsync(stock: 2);

        var act = () => _sut.AddItemAsync(_userId, new AddCartItemRequest(product.Id, 3), default);

        await act.Should().ThrowAsync<ConflictException>();
        (await _store.GetAsync(_userId, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task AddItem_for_unknown_product_returns_not_found()
    {
        var act = () => _sut.AddItemAsync(_userId, new AddCartItemRequest(Guid.NewGuid(), 1), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddItem_for_inactive_product_is_rejected()
    {
        var product = await AddProductAsync();
        product.Deactivate();
        await _db.SaveChangesAsync();

        var act = () => _sut.AddItemAsync(_userId, new AddCartItemRequest(product.Id, 1), default);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Get_uses_current_price_and_flags_unavailable_items()
    {
        var product = await AddProductAsync(price: 10m, stock: 5);
        await _sut.AddItemAsync(_userId, new AddCartItemRequest(product.Id, 4), default);

        // Price changed and stock dropped after the item was added to the cart.
        product.UpdateDetails(product.Name, null, 15m);
        product.ReserveStock(3);
        await _db.SaveChangesAsync();

        var cart = await _sut.GetAsync(_userId, default);

        var item = cart.Items.Should().ContainSingle().Subject;
        item.UnitPrice.Should().Be(15m);
        item.Available.Should().BeFalse();
        cart.Total.Should().Be(0m, "unavailable lines are not part of the payable total");
    }

    [Fact]
    public async Task RemoveItem_removes_line()
    {
        var product = await AddProductAsync();
        await _sut.AddItemAsync(_userId, new AddCartItemRequest(product.Id, 1), default);

        var cart = await _sut.RemoveItemAsync(_userId, product.Id, default);

        cart.Items.Should().BeEmpty();
    }
}
