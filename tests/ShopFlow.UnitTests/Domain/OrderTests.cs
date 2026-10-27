using FluentAssertions;
using ShopFlow.Domain.Common;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Products;

namespace ShopFlow.UnitTests.Domain;

public class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Order PlaceOrder(out Product keyboard, out Product mouse)
    {
        keyboard = Product.Create("KB", "Keyboard", null, 50m, 10);
        mouse = Product.Create("MS", "Mouse", null, 20.50m, 10);
        return Order.Place(CustomerId, "Baku, Nizami st. 1", [(keyboard, 2), (mouse, 1)]);
    }

    [Fact]
    public void Place_snapshots_prices_calculates_total_and_reserves_stock()
    {
        var order = PlaceOrder(out var keyboard, out var mouse);

        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(120.50m);
        order.Items.Should().HaveCount(2);
        keyboard.StockQuantity.Should().Be(8);
        mouse.StockQuantity.Should().Be(9);
    }

    [Fact]
    public void Place_keeps_item_price_when_product_price_changes_later()
    {
        var order = PlaceOrder(out var keyboard, out _);

        keyboard.UpdateDetails("Keyboard", null, 999m);

        order.Items.Single(i => i.ProductId == keyboard.Id).UnitPrice.Should().Be(50m);
    }

    [Fact]
    public void Place_with_insufficient_stock_does_not_reserve_anything()
    {
        var keyboard = Product.Create("KB", "Keyboard", null, 50m, 10);
        var mouse = Product.Create("MS", "Mouse", null, 20m, 1);

        var act = () => Order.Place(CustomerId, "Baku, Nizami st. 1", [(keyboard, 2), (mouse, 5)]);

        act.Should().Throw<InsufficientStockException>();
        keyboard.StockQuantity.Should().Be(10, "validation happens before any stock is reserved");
        mouse.StockQuantity.Should().Be(1);
    }

    [Fact]
    public void Place_without_items_throws()
    {
        var act = () => Order.Place(CustomerId, "Baku, Nizami st. 1", []);

        act.Should().Throw<DomainException>().WithMessage("*at least one item*");
    }

    [Fact]
    public void Happy_path_status_transitions()
    {
        var order = PlaceOrder(out _, out _);

        order.MarkPaid();
        order.Ship();
        order.Deliver();

        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Cannot_ship_unpaid_order()
    {
        var order = PlaceOrder(out _, out _);

        var act = order.Ship;

        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Cannot_pay_twice()
    {
        var order = PlaceOrder(out _, out _);
        order.MarkPaid();

        var act = order.MarkPaid;

        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Cancel_releases_reserved_stock()
    {
        var order = PlaceOrder(out var keyboard, out var mouse);

        order.Cancel(new Dictionary<Guid, Product> { [keyboard.Id] = keyboard, [mouse.Id] = mouse });

        order.Status.Should().Be(OrderStatus.Cancelled);
        keyboard.StockQuantity.Should().Be(10);
        mouse.StockQuantity.Should().Be(10);
    }

    [Fact]
    public void Cannot_cancel_shipped_order()
    {
        var order = PlaceOrder(out var keyboard, out var mouse);
        order.MarkPaid();
        order.Ship();

        var act = () => order.Cancel(new Dictionary<Guid, Product> { [keyboard.Id] = keyboard, [mouse.Id] = mouse });

        act.Should().Throw<InvalidOrderStateException>();
        keyboard.StockQuantity.Should().Be(8);
    }
}
