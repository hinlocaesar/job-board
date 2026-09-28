using JobBoard.Domain.Enums;

namespace JobBoard.Domain.Entities;

public class Job
{
    public Guid Id { get; set; }
    public Guid EmployerProfileId { get; set; }
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    /// <summary>Markdown; sanitized and rendered as HTML by the frontend.</summary>
    public string Description { get; set; } = string.Empty;
    public JobType JobType { get; set; }
    public Region Region { get; set; } = Region.Worldwide;
    public PayType PayType { get; set; } = PayType.Hourly;
    public decimal PayMin { get; set; }
    public decimal PayMax { get; set; }
    public string Currency { get; set; } = "USD";
    public ExperienceLevel ExperienceLevel { get; set; } = ExperienceLevel.Mid;
    public short? HoursPerWeek { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    /// <summary>Why an admin flagged/rejected the posting.</summary>
    public string? StatusReason { get; set; }
    /// <summary>Set when the posting was imported from an external feed (e.g. "remotive"); null for employer-posted jobs.</summary>
    public string? Source { get; set; }
    /// <summary>The id this job has in the external feed. Together with <see cref="Source"/> it makes imports idempotent.</summary>
    public string? SourceId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ClosesAt { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public EmployerProfile? EmployerProfile { get; set; }
    public Category? Category { get; set; }
    public ICollection<JobSkill> Skills { get; set; } = new List<JobSkill>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
}

/// <summary>Skill chips shown on a job and used for filtering.</summary>
public class JobSkill
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string SkillName { get; set; } = string.Empty;

    public Job? Job { get; set; }
}
