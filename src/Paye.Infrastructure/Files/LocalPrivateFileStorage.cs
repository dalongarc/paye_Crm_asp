using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Paye.Core.Configuration;
using Paye.Core.Files;

namespace Paye.Infrastructure.Files;

public sealed partial class LocalPrivateFileStorage(
    IOptions<SecuritySettings> securityOptions) : IPrivateFileStorage
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    private readonly string _rootPath = Path.GetFullPath(securityOptions.Value.PrivateUploadsPath);

    [GeneratedRegex(@"^[a-f0-9]{32}\.(pdf|png|jpg|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeStoredFileNameRegex();

    public async Task<PrivateFileSaveResult> SaveValidatedAsync(
        Stream content,
        string originalFileName,
        long maxBytes = 5 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (maxBytes <= 0 || maxBytes > 25 * 1024 * 1024)
        {
            return new PrivateFileSaveResult(false, ErrorMessage: "Límite de tamaño de archivo inválido.");
        }

        var rawExt = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        var normalizedExt = rawExt switch
        {
            ".pdf" => ".pdf",
            ".png" => ".png",
            ".jpg" or ".jpeg" => ".jpg",
            ".webp" => ".webp",
            _ => null
        };

        if (normalizedExt is null)
        {
            return new PrivateFileSaveResult(
                false,
                ErrorMessage: "Tipo de archivo no permitido. Solo se admiten PDF, PNG, JPEG o WebP.");
        }

        await using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        long totalRead = 0;

        while (true)
        {
            var read = await content.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > maxBytes)
            {
                return new PrivateFileSaveResult(
                    false,
                    ErrorMessage: $"El archivo supera el tamaño máximo permitido ({maxBytes / 1024} KB).");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (totalRead < 8)
        {
            return new PrivateFileSaveResult(
                false,
                ErrorMessage: "El archivo está vacío o incompleto.");
        }

        var headerBytes = buffer.GetBuffer().AsSpan(0, (int)Math.Min(totalRead, 16));
        var detectedContentType = MatchMagicBytes(normalizedExt, headerBytes);
        if (detectedContentType is null)
        {
            return new PrivateFileSaveResult(
                false,
                ErrorMessage: "La firma binaria (magic bytes) del archivo no coincide con el formato declarado.");
        }

        Directory.CreateDirectory(_rootPath);

        var storedFileName = $"{Guid.NewGuid():N}{normalizedExt}";
        var fullFilePath = GetSafeFullPath(storedFileName);
        if (fullFilePath is null)
        {
            return new PrivateFileSaveResult(false, ErrorMessage: "Nombre de archivo generado inválido.");
        }

        buffer.Position = 0;
        await using (var fileStream = new FileStream(
            fullFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16 * 1024,
            useAsync: true))
        {
            await buffer.CopyToAsync(fileStream, cancellationToken);
        }

        var stored = new StoredPrivateFile(
            StoredFileName: storedFileName,
            ContentType: detectedContentType,
            SizeBytes: totalRead,
            UploadedAtUtc: DateTimeOffset.UtcNow);

        return new PrivateFileSaveResult(true, stored);
    }

    public Task<(Stream Stream, string ContentType)?> OpenReadAsync(
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var fullPath = GetSafeFullPath(storedFileName);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult<(Stream Stream, string ContentType)?>(null);
        }

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, useAsync: true);
        return Task.FromResult<(Stream Stream, string ContentType)?>((stream, contentType));
    }

    public Task<bool> DeleteAsync(
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var fullPath = GetSafeFullPath(storedFileName);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    private string? GetSafeFullPath(string? storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) ||
            !SafeStoredFileNameRegex().IsMatch(storedFileName) ||
            !string.Equals(Path.GetFileName(storedFileName), storedFileName, StringComparison.Ordinal))
        {
            return null;
        }

        var combined = Path.GetFullPath(Path.Combine(_rootPath, storedFileName));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        return combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            ? combined
            : null;
    }

    private static string? MatchMagicBytes(string normalizedExt, ReadOnlySpan<byte> header)
    {
        return normalizedExt switch
        {
            ".pdf" when header.Length >= PdfSignature.Length &&
                        header[..PdfSignature.Length].SequenceEqual(PdfSignature)
                => "application/pdf",

            ".png" when header.Length >= PngSignature.Length &&
                        header[..PngSignature.Length].SequenceEqual(PngSignature)
                => "image/png",

            ".jpg" when header.Length >= JpegSignature.Length &&
                        header[..JpegSignature.Length].SequenceEqual(JpegSignature)
                => "image/jpeg",

            ".webp" when header.Length >= 12 &&
                         header[..4].SequenceEqual(RiffSignature) &&
                         header.Slice(8, 4).SequenceEqual(WebpSignature)
                => "image/webp",

            _ => null
        };
    }
}
