using System.Text.Json;
using JobBoard.Domain.Entities;
using JobBoard.Infrastructure.Data;

namespace JobBoard.Api.Services;

public interface IAuditService
{
    Task RecordAsync(string action, string entityType, string entityId, object? details, Guid? actorUserId, string? ip, CancellationToken cancellationToken = default);
}

public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db) => _db = db;

    public async Task RecordAsync(string action, string entityType, string entityId, object? details, Guid? actorUserId, string? ip, CancellationToken cancellationToken = default)
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details),
            ActorUserId = actorUserId,
            Ip = ip,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
