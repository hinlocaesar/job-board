using JobBoard.Domain.Enums;

namespace JobBoard.Domain.Entities;

/// <summary>
/// Single-use, expiring codes for email verification and password reset.
/// Only a SHA-256 hash of the code is persisted.
/// </summary>
public class AuthCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AuthCodePurpose Purpose { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Rotating refresh tokens (JWT auth).</summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? UserAgent { get; set; }
    public string? Ip { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
