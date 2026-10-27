using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShopFlow.Application.Abstractions;
using ShopFlow.Domain.Products;
using ShopFlow.Domain.Users;

namespace ShopFlow.Infrastructure.Persistence;

public static class DbInitializer
{
    /// <summary>Applies pending migrations and seeds the admin account plus a few demo products.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));

        if (config.GetValue("Database:MigrateOnStartup", false))
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(ct);
        }

        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var normalized = User.NormalizeEmail(adminEmail);
            if (!await db.Users.AnyAsync(u => u.Email == normalized, ct))
            {
                var hasher = sp.GetRequiredService<IPasswordHasher>();
                db.Users.Add(User.Register(normalized, hasher.Hash(adminPassword), "Administrator", UserRole.Admin));
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Seeded admin user {AdminEmail}", normalized);
            }
        }

        if (config.GetValue("Seed:DemoProducts", false) && !await db.Products.AnyAsync(ct))
        {
            db.Products.AddRange(
                Product.Create("KB-001", "Mechanical Keyboard", "87-key, hot-swappable switches", 89.90m, 50),
                Product.Create("MS-001", "Wireless Mouse", "Ergonomic, 2.4 GHz + Bluetooth", 34.50m, 120),
                Product.Create("MN-027", "27\" 4K Monitor", "IPS panel, USB-C 65 W", 329.00m, 15),
                Product.Create("HS-010", "Noise Cancelling Headset", "Over-ear, 30 h battery", 149.99m, 30),
                Product.Create("CB-USB", "USB-C Cable 2m", "100 W, braided", 9.99m, 500));
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded demo products");
        }
    }
}
