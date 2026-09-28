using JobBoard.Domain.Enums;

namespace JobBoard.Domain.Entities;

/// <summary>Public employer page: company identity behind posted jobs.</summary>
public class EmployerProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? Country { get; set; }
    public Guid? LogoFileId { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
    public StoredFile? LogoFile { get; set; }
}
