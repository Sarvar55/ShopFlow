using ShopFlow.Api.Middleware;
using ShopFlow.Application.Abstractions;

namespace ShopFlow.Api.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    public bool IsAdmin => accessor.HttpContext?.User.IsInRole("Admin") ?? false;
}

public sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string? CorrelationId => accessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string;
}
