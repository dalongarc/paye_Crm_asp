namespace Paye.Core.Auditing;

public sealed class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ActorId { get; set; } = "system";
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Summary { get; set; }
    public string? IpAddress { get; set; }
}

public interface IAuditLogger
{
    Task LogCriticalActionAsync(
        string action,
        string entityName,
        string? entityId,
        string? summary = null,
        CancellationToken cancellationToken = default);
}
