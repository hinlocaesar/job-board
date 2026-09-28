using JobBoard.Domain.Enums;

namespace JobBoard.Domain.Entities;

/// <summary>
/// Public/private worker profile. Contact details are only exposed to
/// authenticated callers (PH Data Privacy Act, in spirit).
/// </summary>
public class WorkerProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Country { get; set; } = "PH";
    public string? City { get; set; }
    public string? TimeZone { get; set; }
    public short YearsOfExperience { get; set; }
    public decimal RateMin { get; set; }
    public decimal RateMax { get; set; }
    public string Currency { get; set; } = "USD";
    public RatePeriod RatePeriod { get; set; } = RatePeriod.Hour;
    public Availability Availability { get; set; } = Availability.Freelance;
    /// <summary>Public/private toggle: private profiles are never listed.</summary>
    public bool IsPublic { get; set; } = true;
    public Guid? ResumeFileId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkerSkill> Skills { get; set; } = new List<WorkerSkill>();
    public ICollection<WorkExperience> Experiences { get; set; } = new List<WorkExperience>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public StoredFile? ResumeFile { get; set; }
}
