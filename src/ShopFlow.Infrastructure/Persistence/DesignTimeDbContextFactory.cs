using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShopFlow.Infrastructure.Persistence;

/// <summary>Used only by "dotnet ef" tooling to create migrations.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=shopflow;Username=shopflow;Password=shopflow")
            .Options;
        return new AppDbContext(options);
    }
}
