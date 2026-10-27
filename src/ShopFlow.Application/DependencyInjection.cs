using Microsoft.Extensions.DependencyInjection;
using ShopFlow.Application.Auditing;
using ShopFlow.Application.Auth;
using ShopFlow.Application.Cart;
using ShopFlow.Application.Orders;
using ShopFlow.Application.Products;

namespace ShopFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProductService>();
        services.AddScoped<CartService>();
        services.AddScoped<OrderService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AuditLogService>();
        return services;
    }
}
