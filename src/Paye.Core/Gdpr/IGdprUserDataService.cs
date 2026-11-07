namespace Paye.Core.Gdpr;

public sealed record GdprUserExportDto(
    string UserId,
    string Email,
    string FullName,
    bool IsActive,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> Roles,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<GdprAuditItemDto> RecentAuditEntries);

public sealed record GdprAuditItemDto(
    DateTimeOffset OccurredAtUtc,
    string Action,
    string EntityName,
    string? EntityId,
    string? Summary);

public interface IGdprUserDataService
{
    Task<GdprUserExportDto?> ExportUserDataAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> AnonymizeAndDeactivateUserAsync(
        string userId,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
