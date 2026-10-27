using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.Application.Abstractions;
using ShopFlow.Application.Common;
using ShopFlow.Domain.Products;

namespace ShopFlow.Application.Products;

public sealed class ProductService(
    IAppDbContext db,
    ICacheService cache,
    ILogger<ProductService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    // Bumping this counter makes every cached product list stale at once,
    // instead of tracking and deleting each page/search key individually.
    internal const string ListVersionKey = "products:list-version";

    internal static string ProductKey(Guid id) => $"product:{id}";

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var cached = await cache.GetAsync<ProductDto>(ProductKey(id), ct);
        if (cached is not null)
            return cached;

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);

        var dto = product.ToDto();
        await cache.SetAsync(ProductKey(id), dto, CacheTtl, ct);
        return dto;
    }

    public async Task<PagedResult<ProductDto>> ListAsync(string? search, int page, int pageSize, bool includeInactive, CancellationToken ct)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var version = await cache.GetCounterAsync(ListVersionKey, ct);
        var key = $"products:list:v{version}:{includeInactive}:{search?.ToLowerInvariant()}:{page}:{pageSize}";

        var cached = await cache.GetAsync<PagedResult<ProductDto>>(key, ct);
        if (cached is not null)
            return cached;

        var query = db.Products.AsNoTracking();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);
        if (search is not null)
        {
            var term = search.ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var result = new PagedResult<ProductDto>(items.Select(p => p.ToDto()).ToList(), page, pageSize, total);
        await cache.SetAsync(key, result, CacheTtl, ct);
        return result;
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"A product with SKU '{sku}' already exists.");

        var product = Product.Create(request.Sku, request.Name, request.Description, request.Price, request.InitialStock);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Product {ProductId} ({Sku}) created", product.Id, product.Sku);
        await InvalidateAsync([product.Id], ct);
        return product.ToDto();
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);

        // Optimistic concurrency: if someone changed the product after the client read it,
        // SaveChanges fails instead of silently overwriting their change.
        db.SetExpectedVersion(product, request.Version);
        product.UpdateDetails(request.Name, request.Description, request.Price);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Product {ProductId} updated", product.Id);
        await InvalidateAsync([product.Id], ct);
        return product.ToDto();
    }

    public Task<ProductDto> RestockAsync(Guid id, RestockRequest request, CancellationToken ct) =>
        MutateAsync(id, p => p.Restock(request.Quantity), ct);

    public Task<ProductDto> DeactivateAsync(Guid id, CancellationToken ct) =>
        MutateAsync(id, p => p.Deactivate(), ct);

    public Task<ProductDto> ActivateAsync(Guid id, CancellationToken ct) =>
        MutateAsync(id, p => p.Activate(), ct);

    /// <summary>Removes cached entries for the given products and all cached product lists.</summary>
    public async Task InvalidateAsync(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        await cache.RemoveAsync(productIds.Select(ProductKey), ct);
        await cache.IncrementAsync(ListVersionKey, ct);
    }

    private async Task<ProductDto> MutateAsync(Guid id, Action<Product> mutation, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product", id);

        mutation(product);
        await db.SaveChangesAsync(ct);

        await InvalidateAsync([product.Id], ct);
        return product.ToDto();
    }
}
