namespace Paye.Core.Files;

public sealed record StoredPrivateFile(
    string StoredFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc);

public sealed record PrivateFileSaveResult(
    bool Succeeded,
    StoredPrivateFile? File = null,
    string? ErrorMessage = null);

public interface IPrivateFileStorage
{
    Task<PrivateFileSaveResult> SaveValidatedAsync(
        Stream content,
        string originalFileName,
        long maxBytes = 5 * 1024 * 1024,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType)?> OpenReadAsync(
        string storedFileName,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string storedFileName,
        CancellationToken cancellationToken = default);
}
