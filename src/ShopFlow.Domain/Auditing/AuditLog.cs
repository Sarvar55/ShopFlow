namespace ShopFlow.Domain.Auditing;

public enum AuditAction
{
    Insert = 0,
    Update = 1,
    Delete = 2
}

/// <summary>
/// A single, immutable audit record. Rows are only ever inserted: the database rejects
/// UPDATE, DELETE and TRUNCATE on the audit_logs table with a trigger.
/// </summary>
public sealed class AuditLog
{
    private AuditLog() { } // EF Core

    public long Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string EntityType { get; private set; } = null!;
    public string EntityId { get; private set; } = null!;
    public AuditAction Action { get; private set; }

    /// <summary>JSON document with the changed properties: { "Prop": { "old": .., "new": .. } }.</summary>
    public string Changes { get; private set; } = null!;

    public Guid? UserId { get; private set; }
    public string? CorrelationId { get; private set; }

    public static AuditLog Record(
        string entityType, string entityId, AuditAction action, string changes,
        Guid? userId, string? correlationId, DateTimeOffset occurredAt) => new()
    {
        EntityType = entityType,
        EntityId = entityId,
        Action = action,
        Changes = changes,
        UserId = userId,
        CorrelationId = correlationId,
        OccurredAt = occurredAt
    };
}
