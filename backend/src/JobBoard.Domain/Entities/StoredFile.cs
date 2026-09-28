using JobBoard.Domain.Enums;

namespace JobBoard.Domain.Entities;

/// <summary>
/// Blob storage metadata (resume/logo uploads). Bytes live outside the DB;
/// only the pointer, size and checksum are stored here.
/// </summary>
public class StoredFile
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public FilePurpose Purpose { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    /// <summary>Relative blob key inside the storage provider.</summary>
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

/// <summary>Append-only trail for admin moderation (approve/flag/ban).</summary>
public class AuditEvent
{
    public long Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
