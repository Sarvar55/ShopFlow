using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopFlow.Application.Auditing;
using ShopFlow.Application.Common;

namespace ShopFlow.Api.Controllers;

/// <summary>Read-only audit trail. There are deliberately no endpoints to change or delete entries.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/audit-logs")]
public sealed class AuditLogsController(AuditLogService auditLogs) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditLogDto>> Query([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        auditLogs.QueryAsync(query, ct);
}
