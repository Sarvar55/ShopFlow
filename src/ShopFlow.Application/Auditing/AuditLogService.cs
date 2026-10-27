using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShopFlow.Application.Abstractions;
using ShopFlow.Application.Common;
using ShopFlow.Domain.Auditing;

namespace ShopFlow.Application.Auditing;

public sealed record AuditLogDto(
    long Id, DateTimeOffset OccurredAt, string EntityType, string EntityId, AuditAction Action,
    JsonElement Changes, Guid? UserId, string? CorrelationId);

public sealed record AuditLogQuery(
    string? EntityType = null, string? EntityId = null, Guid? UserId = null, string? CorrelationId = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null, int Page = 1, int PageSize = 50);

/// <summary>Read-only access to the audit trail. There is intentionally no write/update/delete API.</summary>
public sealed class AuditLogService(IAppDbContext db)
{
    public async Task<PagedResult<AuditLogDto>> QueryAsync(AuditLogQuery q, CancellationToken ct)
    {
        var (page, pageSize) = Paging.Normalize(q.Page, q.PageSize);

        var query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.EntityType)) query = query.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.EntityId)) query = query.Where(a => a.EntityId == q.EntityId);
        if (q.UserId is not null) query = query.Where(a => a.UserId == q.UserId);
        if (!string.IsNullOrWhiteSpace(q.CorrelationId)) query = query.Where(a => a.CorrelationId == q.CorrelationId);
        if (q.From is not null) query = query.Where(a => a.OccurredAt >= q.From);
        if (q.To is not null) query = query.Where(a => a.OccurredAt < q.To);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(a => new AuditLogDto(
            a.Id, a.OccurredAt, a.EntityType, a.EntityId, a.Action,
            JsonDocument.Parse(a.Changes).RootElement.Clone(), a.UserId, a.CorrelationId)).ToList();

        return new PagedResult<AuditLogDto>(items, page, pageSize, total);
    }
}
