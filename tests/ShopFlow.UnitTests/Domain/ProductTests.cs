using FluentAssertions;
using ShopFlow.Domain.Common;
using ShopFlow.Domain.Products;

namespace ShopFlow.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Create_normalizes_sku_and_starts_active()
    {
        var product = Product.Create("  kb-001 ", " Keyboard ", null, 49.99m, 10);

        product.Sku.Should().Be("KB-001");
        product.Name.Should().Be("Keyboard");
        product.IsActive.Should().BeTrue();
        product.StockQuantity.Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.123)]
    public void Create_rejects_invalid_price(decimal price)
    {
        var act = () => Product.Create("SKU", "Name", null, price, 1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_rejects_negative_stock()
    {
        var act = () => Product.Create("SKU", "Name", null, 10m, -1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ReserveStock_decreases_quantity()
    {
        var product = Product.Create("SKU", "Name", null, 10m, 5);

        product.ReserveStock(3);

        product.StockQuantity.Should().Be(2);
    }

    [Fact]
    public void ReserveStock_more_than_available_throws_and_keeps_stock()
    {
        var product = Product.Create("SKU", "Name", null, 10m, 2);

        var act = () => product.ReserveStock(3);

        act.Should().Throw<InsufficientStockException>()
            .Which.Available.Should().Be(2);
        product.StockQuantity.Should().Be(2);
    }

    [Fact]
    public void ReserveStock_on_inactive_product_throws()
    {
        var product = Product.Create("SKU", "Name", null, 10m, 5);
        product.Deactivate();

        var act = () => product.ReserveStock(1);

        act.Should().Throw<DomainException>().WithMessage("*no longer available*");
    }

    [Fact]
    public void ReleaseStock_returns_quantity()
    {
        var product = Product.Create("SKU", "Name", null, 10m, 5);
        product.ReserveStock(5);

        product.ReleaseStock(2);

        product.StockQuantity.Should().Be(2);
    }
}
