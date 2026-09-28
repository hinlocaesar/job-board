using JobBoard.Domain.Entities;
using JobBoard.Domain.Enums;

namespace JobBoard.Domain.Abstractions;

/// <summary>Blob storage for resumes/logos. Bytes never live in Postgres.</summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, FilePurpose purpose, Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<Stream> ReadAsync(StoredFile file, CancellationToken cancellationToken = default);
    Task DeleteAsync(StoredFile file, CancellationToken cancellationToken = default);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>Central upload limits (validation happens server-side, never only in the browser).</summary>
public static class FileRules
{
    public const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["application/msword"] = ".doc",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
            ["application/rtf"] = ".rtf",
            ["text/rtf"] = ".rtf",
        };

    public static bool IsAllowed(string contentType, string fileName, out string? error)
    {
        if (!AllowedContentTypes.TryGetValue(contentType, out var extension))
        {
            error = $"Content type '{contentType}' is not allowed. Allowed: pdf, doc, docx, rtf.";
            return false;
        }

        if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            error = $"File extension must be {extension}.";
            return false;
        }

        error = null;
        return true;
    }
}
