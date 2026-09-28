using JobBoard.Domain.Enums;

namespace JobBoard.Domain.Entities;

public class Application
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid WorkerProfileId { get; set; }
    public string? CoverLetter { get; set; }
    /// <summary>Resume snapshot taken when the worker applied.</summary>
    public Guid? ResumeFileId { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public string? RejectionReason { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Job? Job { get; set; }
    public WorkerProfile? WorkerProfile { get; set; }
    public StoredFile? ResumeFile { get; set; }
}
