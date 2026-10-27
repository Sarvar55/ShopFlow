using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopFlow.Application.Common;
using ShopFlow.Application.Products;

namespace ShopFlow.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductDto>> List(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        products.ListAsync(search, page, pageSize, includeInactive && User.IsInRole("Admin"), ct);

    [HttpGet("{id:guid}")]
    public Task<ProductDto> Get(Guid id, CancellationToken ct) => products.GetByIdAsync(id, ct);

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        var product = await products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    /// <summary>Updates name, description and price. Send the Version you read; a stale version returns 409.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public Task<ProductDto> Update(Guid id, UpdateProductRequest request, CancellationToken ct) =>
        products.UpdateAsync(id, request, ct);

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/restock")]
    public Task<ProductDto> Restock(Guid id, RestockRequest request, CancellationToken ct) =>
        products.RestockAsync(id, request, ct);

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/deactivate")]
    public Task<ProductDto> Deactivate(Guid id, CancellationToken ct) => products.DeactivateAsync(id, ct);

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/activate")]
    public Task<ProductDto> Activate(Guid id, CancellationToken ct) => products.ActivateAsync(id, ct);
}
