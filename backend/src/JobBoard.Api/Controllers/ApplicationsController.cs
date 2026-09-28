using JobBoard.Api.Common;
using JobBoard.Api.Services;
using JobBoard.Domain.Entities;
using JobBoard.Domain.Enums;
using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Controllers;

[Route("api")]
public sealed class ApplicationsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ApplicationsController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public sealed record ApplicationInput(string? CoverLetter);
    public sealed record ApplicationDecision(string Status, string? RejectionReason);

    public sealed record ApplicantDto(
        Guid Id,
        Guid WorkerProfileId,
        string WorkerSlug,
        string Headline,
        string FullName,
        string? Email,
        string? PhoneNumber,
        string Availability,
        decimal RateMin,
        decimal RateMax,
        string Currency,
        IReadOnlyList<string> Skills,
        Guid? ResumeFileId,
        string? CoverLetter,
        string Status,
        string? RejectionReason,
        DateTime AppliedAt);

    public sealed record MyApplicationDto(
        Guid Id,
        Guid JobId,
        string JobTitle,
        string JobSlug,
        string JobStatus,
        string CompanyName,
        string Status,
        string? RejectionReason,
        DateTime AppliedAt);

    /// <summary>Worker: apply to a published job (one application per worker per job).</summary>
    [HttpPost("jobs/{jobId:guid}/apply")]
    [Authorize(Roles = Roles.Worker)]
    public async Task<IActionResult> Apply(Guid jobId, [FromBody] ApplicationInput input, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var job = await _db.Jobs.Include(j => j.EmployerProfile)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            ?? throw ApiException.NotFound("Job");

        if (job.Status != JobStatus.Published)
            throw ApiException.BadRequest("job_not_open", "This job is not accepting applications.");
        if (job.EmployerProfile?.UserId == userId)
            throw ApiException.Forbidden("You cannot apply to your own job posting.");
        if (job.ClosesAt is { } closesAt && closesAt < DateTime.UtcNow)
            throw ApiException.BadRequest("job_closed", "This job's application window has closed.");

        var profile = await _db.WorkerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw ApiException.BadRequest("worker_profile_required", "Create your worker profile before applying.");

        if (await _db.Applications.AnyAsync(a => a.JobId == job.Id && a.WorkerProfileId == profile.Id, cancellationToken))
            throw ApiException.Conflict("already_applied", "You have already applied to this job.");

        var application = new Application
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            WorkerProfileId = profile.Id,
            CoverLetter = string.IsNullOrWhiteSpace(input.CoverLetter) ? null : input.CoverLetter.Trim(),
            ResumeFileId = profile.ResumeFileId, // snapshot at apply time
            Status = ApplicationStatus.Submitted,
        };

        _db.Applications.Add(application);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("application.submitted", "application", application.Id.ToString(), new { job.Id }, CurrentUserId, ClientIp, cancellationToken);

        return CreatedAtAction(nameof(MyApplications), null, new { id = application.Id, status = application.Status.ToString() });
    }

    /// <summary>Employer: applicants for one of my jobs (contact details included — caller is authenticated).</summary>
    [HttpGet("jobs/{jobId:guid}/applications")]
    [Authorize(Roles = $"{Roles.Employer},{Roles.Admin}")]
    public async Task<ActionResult<IReadOnlyList<ApplicantDto>>> Applicants(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _db.Jobs.Include(j => j.EmployerProfile)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            ?? throw ApiException.NotFound("Job");

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole(Roles.Admin);
        if (!isAdmin && job.EmployerProfile?.UserId != userId)
            throw ApiException.Forbidden("You can only view applicants for your own jobs.");

        var applications = await _db.Applications.AsNoTracking()
            .Where(a => a.JobId == jobId)
            .Include(a => a.WorkerProfile).ThenInclude(w => w!.Skills).ThenInclude(s => s.Skill)
            .AsSplitQuery()
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(cancellationToken);

        var userIds = applications.Select(a => a.WorkerProfile!.UserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var result = applications.Select(application =>
        {
            var worker = application.WorkerProfile!;
            users.TryGetValue(worker.UserId, out var account);
            return new ApplicantDto(
                application.Id,
                worker.Id,
                worker.Slug,
                worker.Headline,
                account?.FullName ?? string.Empty,
                account?.Email,
                account?.PhoneNumber,
                worker.Availability.ToString(),
                worker.RateMin,
                worker.RateMax,
                worker.Currency,
                worker.Skills.Select(s => s.Skill?.Name ?? string.Empty).Where(n => n.Length > 0).ToList(),
                application.ResumeFileId,
                application.CoverLetter,
                application.Status.ToString(),
                application.RejectionReason,
                application.AppliedAt);
        }).ToList();

        return Ok(result);
    }

    /// <summary>Employer: shortlist or reject an applicant.</summary>
    [HttpPatch("applications/{id:guid}")]
    [Authorize(Roles = Roles.Employer)]
    public async Task<IActionResult> Decide(Guid id, [FromBody] ApplicationDecision decision, CancellationToken cancellationToken)
    {
        var status = EnumValue<ApplicationStatus>.Parse(decision.Status, nameof(decision.Status));
        if (status is not (ApplicationStatus.Shortlisted or ApplicationStatus.Rejected))
            throw ApiException.BadRequest("invalid_status", "Status must be 'Shortlisted' or 'Rejected'.");

        var application = await RequireOwnApplicationAsync(id, cancellationToken);
        application.Status = status;
        application.RejectionReason = status == ApplicationStatus.Rejected
            ? decision.RejectionReason?.Trim()
            : null;
        application.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync($"application.{status.ToString().ToLowerInvariant()}", "application", application.Id.ToString(), new { decision.RejectionReason }, CurrentUserId, ClientIp, cancellationToken);

        return Ok(new { id = application.Id, status = application.Status.ToString() });
    }

    /// <summary>Worker: withdraw one of my applications.</summary>
    [HttpPatch("applications/{id:guid}/withdraw")]
    [Authorize(Roles = Roles.Worker)]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var application = await _db.Applications
            .Include(a => a.Job).ThenInclude(j => j!.EmployerProfile)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Application");

        if (application.WorkerProfile!.UserId != userId)
            throw ApiException.Forbidden("You can only withdraw your own applications.");
        if (application.Status == ApplicationStatus.Withdrawn)
            return Ok(new { id = application.Id, status = application.Status.ToString() });

        application.Status = ApplicationStatus.Withdrawn;
        application.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { id = application.Id, status = application.Status.ToString() });
    }

    /// <summary>Worker: everything I have applied to.</summary>
    [HttpGet("applications/mine")]
    [Authorize(Roles = Roles.Worker)]
    public async Task<ActionResult<IReadOnlyList<MyApplicationDto>>> MyApplications(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var applications = await _db.Applications.AsNoTracking()
            .Where(a => a.WorkerProfile!.UserId == userId)
            .Include(a => a.Job)
            .AsSplitQuery()
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync(cancellationToken);

        return Ok(applications.Select(a => new MyApplicationDto(
            a.Id,
            a.JobId,
            a.Job?.Title ?? string.Empty,
            a.Job?.Slug ?? string.Empty,
            a.Job?.Status.ToString() ?? string.Empty,
            a.Job?.EmployerProfile?.CompanyName ?? string.Empty,
            a.Status.ToString(),
            a.RejectionReason,
            a.AppliedAt)).ToList());
    }

    private async Task<Application> RequireOwnApplicationAsync(Guid id, CancellationToken cancellationToken)
    {
        var application = await _db.Applications
            .Include(a => a.Job).ThenInclude(j => j!.EmployerProfile)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Application");

        if (application.Job!.EmployerProfile?.UserId != CurrentUserId)
            throw ApiException.Forbidden("You can only manage applicants for your own jobs.");

        return application;
    }
}
