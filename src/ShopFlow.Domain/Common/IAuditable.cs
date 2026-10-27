namespace ShopFlow.Domain.Common;

/// <summary>
/// Marker for entities whose every insert/update/delete is written to the append-only audit log.
/// </summary>
public interface IAuditable;

/// <summary>
/// Entities with CreatedAt/UpdatedAt columns maintained by the persistence layer.
/// </summary>
public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset UpdatedAt { get; }
}

/// <summary>
/// Excludes a property (e.g. password hash) from audit log change sets.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute;
