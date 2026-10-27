using Microsoft.EntityFrameworkCore;
using ShopFlow.Application.Abstractions;
using ShopFlow.Application.Common;

namespace ShopFlow.Application.Cart;

/// <summary>
/// The cart lives in Redis (fast, expiring, no DB writes per click). Prices are never stored in the
/// cart: they are always read from the database, so the customer sees and pays the current price.
/// </summary>
public sealed class CartService(IAppDbContext db, ICartStore store)
{
    public const int MaxQuantityPerLine = 100;

    public async Task<CartDto> GetAsync(Guid userId, CancellationToken ct)
    {
        var lines = await store.GetAsync(userId, ct);
        if (lines.Count == 0)
            return new CartDto([], 0m);

        var ids = lines.Keys.ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var items = new List<CartItemDto>();
        foreach (var (productId, quantity) in lines)
        {
            // Products that were removed from the catalog are silently dropped from the view.
            if (!products.TryGetValue(productId, out var p))
                continue;

            var available = p.IsActive && p.StockQuantity >= quantity;
            items.Add(new CartItemDto(p.Id, p.Sku, p.Name, p.Price, quantity, p.Price * quantity, available));
        }

        return new CartDto(items.OrderBy(i => i.Name).ToList(), items.Where(i => i.Available).Sum(i => i.LineTotal));
    }

    public async Task<CartDto> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken ct)
    {
        var current = await store.GetAsync(userId, ct);
        var newQuantity = request.Quantity + current.GetValueOrDefault(request.ProductId);

        await EnsureAvailableAsync(request.ProductId, newQuantity, ct);
        await store.SetQuantityAsync(userId, request.ProductId, newQuantity, ct);
        return await GetAsync(userId, ct);
    }

    public async Task<CartDto> UpdateItemAsync(Guid userId, Guid productId, UpdateCartItemRequest request, CancellationToken ct)
    {
        var current = await store.GetAsync(userId, ct);
        if (!current.ContainsKey(productId))
            throw new NotFoundException("Cart item", productId);

        await EnsureAvailableAsync(productId, request.Quantity, ct);
        await store.SetQuantityAsync(userId, productId, request.Quantity, ct);
        return await GetAsync(userId, ct);
    }

    public async Task<CartDto> RemoveItemAsync(Guid userId, Guid productId, CancellationToken ct)
    {
        await store.RemoveAsync(userId, productId, ct);
        return await GetAsync(userId, ct);
    }

    public Task ClearAsync(Guid userId, CancellationToken ct) => store.ClearAsync(userId, ct);

    private async Task EnsureAvailableAsync(Guid productId, int quantity, CancellationToken ct)
    {
        if (quantity > MaxQuantityPerLine)
            throw new ValidationException($"At most {MaxQuantityPerLine} units of a product can be added to the cart.");

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new NotFoundException("Product", productId);

        if (!product.IsActive)
            throw new ValidationException($"Product '{product.Sku}' is not available.");

        // Soft check only: stock is not reserved until checkout, where it is re-checked under a row lock.
        if (product.StockQuantity < quantity)
            throw new ConflictException($"Only {product.StockQuantity} unit(s) of '{product.Sku}' are in stock.");
    }
}
