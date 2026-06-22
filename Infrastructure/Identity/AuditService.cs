using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Identity;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _current;

    public AuditService(AppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task LogAsync(string action, string entityName, string entityId, string? detail = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new Domain.Entities.AuditLog
        {
            UserId = _current.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Detail = detail,
            CorrelationId = _current.CorrelationId,
        });
        await _db.SaveChangesAsync(ct);
    }
}
