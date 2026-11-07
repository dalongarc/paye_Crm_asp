using Paye.Core.Auditing;
using Paye.Core.Common;
using Paye.Infrastructure.Data;

namespace Paye.Infrastructure.Auditing;

public sealed class DatabaseAuditLogger(
    PayeDbContext dbContext,
    ICurrentUserService currentUserService) : IAuditLogger
{
    public async Task LogCriticalActionAsync(
        string action,
        string entityName,
        string? entityId,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("La acción de auditoría es obligatoria.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("El nombre de entidad de auditoría es obligatorio.", nameof(entityName));
        }

        var safeSummary = summary is { Length: > 500 }
            ? summary[..500]
            : summary;

        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ActorId = currentUserService.GetActorIdentifier(),
            ActorEmail = currentUserService.UserEmail,
            IpAddress = currentUserService.IpAddress,
            Action = action.Trim(),
            EntityName = entityName.Trim(),
            EntityId = entityId?.Trim(),
            Summary = safeSummary
        };

        dbContext.AuditLogs.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
