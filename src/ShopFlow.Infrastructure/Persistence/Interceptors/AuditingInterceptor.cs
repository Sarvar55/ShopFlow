using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShopFlow.Application.Abstractions;
using ShopFlow.Domain.Auditing;
using ShopFlow.Domain.Common;

namespace ShopFlow.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Runs right before every SaveChanges:
///  1. stamps CreatedAt / UpdatedAt,
///  2. refuses to modify or delete existing audit rows (append-only, also enforced by a DB trigger),
///  3. adds an <see cref="AuditLog"/> row for every changed <see cref="IAuditable"/> entity.
/// Because the audit rows are part of the same SaveChanges, they are written in the same
/// transaction as the business change: either both are committed or neither is.
/// </summary>
public sealed class AuditingInterceptor(
    ICurrentUser currentUser,
    ICorrelationContext correlation,
    TimeProvider clock) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
    private static readonly HashSet<string> IgnoredProperties = ["Version", "CreatedAt", "UpdatedAt"];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            OnSaving(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            OnSaving(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void OnSaving(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var now = clock.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Audit log entries are append-only and cannot be modified or deleted.");
        }

        var auditRows = new List<AuditLog>();
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            StampTimestamps(entry, now);

            if (entry.Entity is not IAuditable)
                continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Insert,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => (AuditAction?)null
            };
            if (action is null)
                continue;

            var changes = CollectChanges(entry, action.Value);
            if (action == AuditAction.Update && changes.Count == 0)
                continue;

            auditRows.Add(AuditLog.Record(
                entry.Metadata.ClrType.Name,
                entry.Property("Id").CurrentValue!.ToString()!,
                action.Value,
                JsonSerializer.Serialize(changes, JsonOptions),
                currentUser.UserId,
                correlation.CorrelationId,
                now));
        }

        context.Set<AuditLog>().AddRange(auditRows);
    }

    private static void StampTimestamps(EntityEntry entry, DateTimeOffset now)
    {
        if (entry.Entity is not IHasTimestamps)
            return;

        if (entry.State == EntityState.Added)
        {
            entry.Property(nameof(IHasTimestamps.CreatedAt)).CurrentValue = now;
            entry.Property(nameof(IHasTimestamps.UpdatedAt)).CurrentValue = now;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Property(nameof(IHasTimestamps.UpdatedAt)).CurrentValue = now;
        }
    }

    private static Dictionary<string, object?> CollectChanges(EntityEntry entry, AuditAction action)
    {
        var changes = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (name == "Id" || IgnoredProperties.Contains(name) || IsIgnored(property))
                continue;

            switch (action)
            {
                case AuditAction.Insert:
                    changes[name] = new { @new = property.CurrentValue };
                    break;
                case AuditAction.Delete:
                    changes[name] = new { old = property.OriginalValue };
                    break;
                case AuditAction.Update when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[name] = new { old = property.OriginalValue, @new = property.CurrentValue };
                    break;
            }
        }
        return changes;
    }

    private static bool IsIgnored(PropertyEntry property) =>
        property.Metadata.PropertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>() is not null;
}
