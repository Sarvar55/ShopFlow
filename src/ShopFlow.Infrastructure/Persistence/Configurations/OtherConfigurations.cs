using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopFlow.Domain.Auditing;
using ShopFlow.Domain.Orders;
using ShopFlow.Domain.Payments;
using ShopFlow.Domain.Users;

namespace ShopFlow.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
        b.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasKey(p => p.Id);
        b.Property(p => p.Amount).HasPrecision(12, 2);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(p => p.ProviderReference).HasMaxLength(100);
        b.Property(p => p.FailureReason).HasMaxLength(500);
        b.HasOne<Order>().WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.OrderId);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).UseIdentityAlwaysColumn();
        b.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        b.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        b.Property(a => a.Action).HasConversion<string>().HasMaxLength(10);
        b.Property(a => a.Changes).HasColumnType("jsonb").IsRequired();
        b.Property(a => a.CorrelationId).HasMaxLength(100);
        b.HasIndex(a => new { a.EntityType, a.EntityId });
        b.HasIndex(a => a.OccurredAt);
        b.HasIndex(a => a.CorrelationId);
    }
}
