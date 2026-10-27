using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShopFlow.Application.Abstractions;
using ShopFlow.Application.Common;
using ShopFlow.Domain.Auditing;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Payments;
using ShopFlow.Domain.Products;
using ShopFlow.Domain.Users;

namespace ShopFlow.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public void SetExpectedVersion(Product product, uint expectedVersion) =>
        Entry(product).Property(p => p.Version).OriginalValue = expectedVersion;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            // Translate to an application-level exception so services don't depend on Npgsql.
            throw new UniqueConstraintException(pg.ConstraintName, ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySnakeCaseColumnNames(modelBuilder);
    }

    // PostgreSQL convention: snake_case identifiers, so raw SQL does not need quoted names.
    private static void ApplySnakeCaseColumnNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                // uint row versions are mapped by Npgsql to the xmin system column; keep that name.
                if (property.IsConcurrencyToken && property.ClrType == typeof(uint))
                    continue;
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
