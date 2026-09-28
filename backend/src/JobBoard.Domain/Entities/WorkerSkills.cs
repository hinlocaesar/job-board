namespace JobBoard.Domain.Entities;

/// <summary>Controlled vocabulary of skills (deduplicated, lower-cased).</summary>
public class Skill
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<WorkerSkill> WorkerSkills { get; set; } = new List<WorkerSkill>();
}

public class WorkerSkill
{
    public Guid Id { get; set; }
    public Guid WorkerProfileId { get; set; }
    public Guid SkillId { get; set; }
    /// <summary>Self-assessed level, 1..5.</summary>
    public byte Level { get; set; } = 3;
    public short YearsExperience { get; set; }

    public WorkerProfile? WorkerProfile { get; set; }
    public Skill? Skill { get; set; }
}

public class WorkExperience
{
    public Guid Id { get; set; }
    public Guid WorkerProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public WorkerProfile? WorkerProfile { get; set; }
}
