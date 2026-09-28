using JobBoard.Api.Common;
using JobBoard.Api.Services;
using JobBoard.Domain.Enums;
using JobBoard.Infrastructure.Data;
using JobBoard.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Controllers;

/// <summary>Basic moderation: approve/flag jobs, ban users, inspect the audit trail.</summary>
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(AppDbContext db, IAuditService audit, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _audit = audit;
        _userManager = userManager;
    }

    public sealed record JobQueueDto(
        Guid Id, string Title, string Slug, string Status, string? StatusReason,
        string CompanyName, string CategoryName, string JobType,
        decimal PayMin, decimal PayMax, string Currency, int ViewCount, int ApplicantCount,
        DateTime CreatedAt, DateTime? PublishedAt);

    public sealed record ModerationInput(string? Reason);
    public sealed record UserSummaryDto(
        Guid Id, string Email, string? FullName, IReadOnlyList<string> Roles,
        string AccountStatus, bool EmailVerified, DateTime CreatedAt,
        int JobCount, int ApplicationCount);

    public sealed record AuditDto(long Id, Guid? ActorUserId, string Action, string EntityType, string EntityId, string? Details, string? Ip, DateTime CreatedAt);

    /// <summary>Queue of postings, optionally filtered by status (defaults to Pending).</summary>
    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<JobQueueDto>>> Jobs([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var filter = string.IsNullOrWhiteSpace(status)
            ? JobStatus.Pending
            : EnumValue<JobStatus>.Parse(status, nameof(status));

        var jobs = await _db.Jobs.AsNoTracking()
            .Where(j => j.Status == filter)
            .Include(j => j.Category)
            .Include(j => j.EmployerProfile)
            .Include(j => j.Applications)
            .AsSplitQuery()
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(jobs.Select(j => new JobQueueDto(
            j.Id, j.Title, j.Slug, j.Status.ToString(), j.StatusReason,
            j.EmployerProfile?.CompanyName ?? string.Empty, j.Category?.Name ?? string.Empty, j.JobType.ToString(),
            j.PayMin, j.PayMax, j.Currency, j.ViewCount, j.Applications.Count,
            j.CreatedAt, j.PublishedAt)).ToList());
    }

    [HttpPost("jobs/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Job");

        if (job.Status is JobStatus.Rejected or JobStatus.Flagged or JobStatus.Closed)
            throw ApiException.BadRequest("invalid_status", $"A job in status '{job.Status}' cannot be approved directly.");

        job.Status = JobStatus.Published;
        job.StatusReason = null;
        job.PublishedAt ??= DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("job.approved", "job", job.Id.ToString(), new { job.Title }, CurrentUserId, ClientIp, cancellationToken);

        return Ok(new { id = job.Id, status = job.Status.ToString() });
    }

    [HttpPost("jobs/{id:guid}/flag")]
    public async Task<IActionResult> Flag(Guid id, [FromBody] ModerationInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw ApiException.BadRequest("reason_required", "A reason is required when flagging a job.");

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Job");

        job.Status = JobStatus.Flagged;
        job.StatusReason = input.Reason.Trim();
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("job.flagged", "job", job.Id.ToString(), new { input.Reason }, CurrentUserId, ClientIp, cancellationToken);

        return Ok(new { id = job.Id, status = job.Status.ToString() });
    }

    [HttpPost("jobs/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ModerationInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw ApiException.BadRequest("reason_required", "A reason is required when rejecting a job.");

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Job");

        job.Status = JobStatus.Rejected;
        job.StatusReason = input.Reason.Trim();
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("job.rejected", "job", job.Id.ToString(), new { input.Reason }, CurrentUserId, ClientIp, cancellationToken);

        return Ok(new { id = job.Id, status = job.Status.ToString() });
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> Users([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.Email!.Contains(term) || (u.FullName != null && u.FullName.Contains(term)));
        }

        var users = await query.OrderByDescending(u => u.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var ids = users.Select(u => u.Id).ToList();

        var jobCounts = await _db.Jobs.AsNoTracking()
            .Where(j => j.EmployerProfile != null && ids.Contains(j.EmployerProfile.UserId))
            .GroupBy(j => j.EmployerProfile!.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var applicationCounts = await _db.Applications.AsNoTracking()
            .Where(a => a.WorkerProfile != null && ids.Contains(a.WorkerProfile.UserId))
            .GroupBy(a => a.WorkerProfile!.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var result = new List<UserSummaryDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserSummaryDto(
                user.Id, user.Email ?? string.Empty, user.FullName, roles.ToList(),
                user.AccountStatus.ToString(), user.EmailConfirmed, user.CreatedAt,
                jobCounts.GetValueOrDefault(user.Id),
                applicationCounts.GetValueOrDefault(user.Id)));
        }

        return Ok(result);
    }

    [HttpPost("users/{id:guid}/ban")]
    public async Task<IActionResult> Ban(Guid id, [FromBody] ModerationInput input, CancellationToken cancellationToken)
    {
        if (id == CurrentUserId)
            throw ApiException.BadRequest("cannot_ban_self", "You cannot ban your own account.");

        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw ApiException.NotFound("User");

        if (await _userManager.IsInRoleAsync(user, Roles.Admin))
            throw ApiException.Forbidden("Administrators cannot be banned.");

        user.AccountStatus = AccountStatus.Banned;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Kill any live sessions immediately.
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(cancellationToken);
        tokens.ForEach(t => t.RevokedAt = DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync("user.banned", "user", user.Id.ToString(), new { input.Reason }, CurrentUserId, ClientIp, cancellationToken);
        return Ok(new { id = user.Id, status = user.AccountStatus.ToString() });
    }

    [HttpPost("users/{id:guid}/unban")]
    public async Task<IActionResult> Unban(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw ApiException.NotFound("User");

        if (user.AccountStatus is not (AccountStatus.Banned or AccountStatus.Suspended))
            throw ApiException.BadRequest("invalid_status", "This account is not banned or suspended.");

        user.AccountStatus = AccountStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        await _audit.RecordAsync("user.unbanned", "user", user.Id.ToString(), null, CurrentUserId, ClientIp, cancellationToken);

        return Ok(new { id = user.Id, status = user.AccountStatus.ToString() });
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyList<AuditDto>>> Audit([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var events = await _db.AuditEvents.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(e => new AuditDto(e.Id, e.ActorUserId, e.Action, e.EntityType, e.EntityId, e.Details, e.Ip, e.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(events);
    }
}
