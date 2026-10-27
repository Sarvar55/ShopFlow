using System.ComponentModel.DataAnnotations;
using ShopFlow.Domain.Products;

namespace ShopFlow.Application.Products;

public sealed record ProductDto(
    Guid Id, string Sku, string Name, string? Description, decimal Price,
    int StockQuantity, bool IsActive, uint Version);

public sealed record CreateProductRequest(
    [Required, StringLength(Product.MaxSkuLength)] string Sku,
    [Required, StringLength(Product.MaxNameLength)] string Name,
    [StringLength(2000)] string? Description,
    [Range(0.01, 1_000_000)] decimal Price,
    [Range(0, 1_000_000)] int InitialStock);

public sealed record UpdateProductRequest(
    [Required, StringLength(Product.MaxNameLength)] string Name,
    [StringLength(2000)] string? Description,
    [Range(0.01, 1_000_000)] decimal Price,
    uint Version);

public sealed record RestockRequest([Range(1, 1_000_000)] int Quantity);

internal static class ProductMappings
{
    public static ProductDto ToDto(this Product p) =>
        new(p.Id, p.Sku, p.Name, p.Description, p.Price, p.StockQuantity, p.IsActive, p.Version);
}
