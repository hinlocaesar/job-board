using JobBoard.Api.Common;
using JobBoard.Api.Features.Jobs;
using JobBoard.Api.Services;
using JobBoard.Domain.Abstractions;
using JobBoard.Domain.Entities;
using JobBoard.Domain.Enums;
using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Controllers;

[Route("api/jobs")]
public sealed class JobsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJobSearchService _search;
    private readonly IAuditService _audit;

    public JobsController(AppDbContext db, IJobSearchService search, IAuditService audit)
    {
        _db = db;
        _search = search;
        _audit = audit;
    }

    public sealed record JobQuery(
        string? q,
        string? category,
        string? jobType,
        string? experience,
        decimal? minPay,
        string? payType,
        string? currency,
        string? sort,
        int page = 1,
        int pageSize = 10);

    /// <summary>Public job board: search, filter by category/type/pay, paginated.</summary>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<ActionResult<JobSearchResult>> List([FromQuery] JobQuery query, CancellationToken cancellationToken)
    {
        Guid? categoryId = null;
        if (!string.IsNullOrWhiteSpace(query.category))
        {
            if (Guid.TryParse(query.category, out var parsed))
                categoryId = parsed;
            else
                categoryId = (await _db.Categories.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Slug == query.category, cancellationToken))?.Id
                    ?? throw ApiException.NotFound("Category");
        }

        var criteria = new JobSearchCriteria
        {
            Query = query.q,
            CategoryId = categoryId,
            JobType = ParseOptional<JobType>(query.jobType, nameof(query.jobType)),
            ExperienceLevel = ParseOptional<ExperienceLevel>(query.experience, nameof(query.experience)),
            MinPay = query.minPay,
            PayType = ParseOptional<PayType>(query.payType, nameof(query.payType)),
            Currency = query.currency,
            Sort = string.IsNullOrWhiteSpace(query.sort) ? "newest" : query.sort!,
            Page = query.page,
            PageSize = query.pageSize,
        };

        return Ok(await _search.SearchAsync(criteria, cancellationToken));
    }

    /// <summary>Public detail page (accepts a slug or a GUID). Owner/admin can preview non-published jobs.</summary>
    [HttpGet("{slugOrId}")]
    [AllowAnonymous]
    public async Task<ActionResult<JobDetailDto>> Detail(string slugOrId, CancellationToken cancellationToken)
    {
        var query = _db.Jobs
            .AsSplitQuery()
            .Include(j => j.EmployerProfile)
            .Include(j => j.Category)
            .Include(j => j.Skills)
            .Include(j => j.Applications);

        Job? job = Guid.TryParse(slugOrId, out var id)
            ? await query.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(j => j.Slug == slugOrId, cancellationToken);

        if (job is null)
            throw ApiException.NotFound("Job");

        var userId = User.Identity?.IsAuthenticated == true ? CurrentUserId : (Guid?)null;
        var isOwner = userId is not null && job.EmployerProfile?.UserId == userId;
        var isAdmin = User.IsInRole(Roles.Admin);

        var isPubliclyVisible = job.Status == JobStatus.Published;
        if (!isPubliclyVisible && !isOwner && !isAdmin)
            throw ApiException.NotFound("Job");

        if (isPubliclyVisible && !isOwner && !isAdmin)
        {
            job.ViewCount++;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(ToDetailDto(job, canEdit: isOwner || isAdmin, includeApplicantCount: isOwner || isAdmin));
    }

    /// <summary>Employer: my postings with applicant counts.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Employer)]
    public async Task<ActionResult<IReadOnlyList<JobMineDto>>> Mine(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var jobs = await _db.Jobs.AsNoTracking()
            .Where(j => j.EmployerProfile!.UserId == userId)
            .Include(j => j.Category)
            .Include(j => j.Applications)
            .AsSplitQuery()
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(jobs.Select(j => new JobMineDto(
            j.Id, j.Title, j.Slug, j.Status.ToString(), j.StatusReason, j.Category?.Name ?? string.Empty,
            j.JobType.ToString(), j.PayType.ToString(), j.PayMin, j.PayMax, j.Currency,
            j.PublishedAt, j.CreatedAt, j.ViewCount, j.Applications.Count)).ToList());
    }

    /// <summary>Employer: post a job. It enters the moderation queue as <c>Pending</c>.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Employer)]
    public async Task<ActionResult<JobMessage>> Create([FromBody] JobInput input, CancellationToken cancellationToken)
    {
        await ValidateAsync(input);
        var employer = await RequireOwnEmployerProfileAsync(cancellationToken);

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == input.CategoryId && c.IsActive, cancellationToken)
            ?? throw ApiException.BadRequest("invalid_category", "The selected category does not exist or is inactive.");

        var job = new Job
        {
            Id = Guid.NewGuid(),
            EmployerProfileId = employer.Id,
            CategoryId = category.Id,
            Title = input.Title.Trim(),
            Slug = await ProfilesController.UniqueSlugAsync(_db, "j", input.Title, cancellationToken),
            Description = input.Description.Trim(),
            JobType = EnumValue<JobType>.Parse(input.JobType, nameof(input.JobType)),
            Region = EnumValue<Region>.Parse(input.Region, nameof(input.Region)),
            PayType = EnumValue<PayType>.Parse(input.PayType, nameof(input.PayType)),
            PayMin = input.PayMin,
            PayMax = input.PayMax,
            Currency = input.Currency.Trim().ToUpperInvariant(),
            ExperienceLevel = EnumValue<ExperienceLevel>.Parse(input.ExperienceLevel, nameof(input.ExperienceLevel)),
            HoursPerWeek = input.HoursPerWeek,
            ClosesAt = input.ClosesAt,
            Status = JobStatus.Pending,
        };

        AddJobSkills(job, input.Skills);
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("job.created", "job", job.Id.ToString(), new { job.Title, job.Status }, CurrentUserId, ClientIp, cancellationToken);

        return CreatedAtAction(nameof(Detail), new { slugOrId = job.Slug }, new JobMessage(false, job.Slug, job.Status.ToString()));
    }

    /// <summary>Employer: edit a posting. A previously rejected job re-enters moderation.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Employer)]
    public async Task<ActionResult<JobMessage>> Update(Guid id, [FromBody] JobInput input, CancellationToken cancellationToken)
    {
        await ValidateAsync(input);
        var job = await RequireOwnJobAsync(id, cancellationToken);

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == input.CategoryId && c.IsActive, cancellationToken)
            ?? throw ApiException.BadRequest("invalid_category", "The selected category does not exist or is inactive.");

        job.Title = input.Title.Trim();
        job.Description = input.Description.Trim();
        job.CategoryId = category.Id;
        job.JobType = EnumValue<JobType>.Parse(input.JobType, nameof(input.JobType));
        job.Region = EnumValue<Region>.Parse(input.Region, nameof(input.Region));
        job.PayType = EnumValue<PayType>.Parse(input.PayType, nameof(input.PayType));
        job.PayMin = input.PayMin;
        job.PayMax = input.PayMax;
        job.Currency = input.Currency.Trim().ToUpperInvariant();
        job.ExperienceLevel = EnumValue<ExperienceLevel>.Parse(input.ExperienceLevel, nameof(input.ExperienceLevel));
        job.HoursPerWeek = input.HoursPerWeek;
        job.ClosesAt = input.ClosesAt;
        job.UpdatedAt = DateTime.UtcNow;

        if (job.Status == JobStatus.Rejected)
        {
            job.Status = JobStatus.Pending;
            job.StatusReason = null;
        }

        var existingSkills = await _db.JobSkills.Where(s => s.JobId == job.Id).ToListAsync(cancellationToken);
        _db.JobSkills.RemoveRange(existingSkills);
        AddJobSkills(job, input.Skills);

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("job.updated", "job", job.Id.ToString(), new { job.Title, job.Status }, CurrentUserId, ClientIp, cancellationToken);

        return Ok(new JobMessage(job.Status == JobStatus.Published, job.Slug, job.Status.ToString()));
    }

    /// <summary>Employer: close a posting (removes it from the public board).</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = Roles.Employer)]
    public async Task<ActionResult<JobMessage>> Close(Guid id, CancellationToken cancellationToken)
    {
        var job = await RequireOwnJobAsync(id, cancellationToken);
        if (job.Status is not (JobStatus.Pending or JobStatus.Published))
            throw ApiException.BadRequest("invalid_status", $"A job in status '{job.Status}' cannot be closed.");

        job.Status = JobStatus.Closed;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("job.closed", "job", job.Id.ToString(), null, CurrentUserId, ClientIp, cancellationToken);

        return Ok(new JobMessage(false, job.Slug, job.Status.ToString()));
    }

    // ---------- helpers ----------

    private async Task<EmployerProfile> RequireOwnEmployerProfileAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        return await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw ApiException.BadRequest("employer_profile_required", "Create your employer profile before posting a job.");
    }

    private async Task<Job> RequireOwnJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await _db.Jobs.Include(j => j.EmployerProfile)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Job");

        var userId = CurrentUserId;
        if (job.EmployerProfile?.UserId != userId)
            throw ApiException.Forbidden("You can only manage your own job postings.");

        return job;
    }

    private void AddJobSkills(Job job, IReadOnlyList<string> skills)
    {
        foreach (var name in skills
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20))
        {
            _db.JobSkills.Add(new JobSkill { Id = Guid.NewGuid(), JobId = job.Id, SkillName = name });
        }
    }

    private static T? ParseOptional<T>(string? value, string fieldName) where T : struct, Enum
        => string.IsNullOrWhiteSpace(value) ? null : EnumValue<T>.Parse(value, fieldName);

    private static JobDetailDto ToDetailDto(Job job, bool canEdit, bool includeApplicantCount) => new(
        job.Id,
        job.Title,
        job.Slug,
        job.Description,
        job.CategoryId,
        job.Category?.Name ?? string.Empty,
        job.Category?.Slug ?? string.Empty,
        job.EmployerProfileId,
        job.EmployerProfile?.CompanyName ?? string.Empty,
        job.EmployerProfile?.Slug ?? string.Empty,
        job.EmployerProfile?.Website,
        job.JobType.ToString(),
        job.Region.ToString(),
        job.PayType.ToString(),
        job.PayMin,
        job.PayMax,
        job.Currency,
        job.ExperienceLevel.ToString(),
        job.HoursPerWeek,
        job.Status.ToString(),
        job.StatusReason,
        job.PublishedAt,
        job.ClosesAt,
        job.ViewCount,
        includeApplicantCount ? job.Applications.Count : 0,
        job.Skills.Select(s => s.SkillName).ToList(),
        canEdit,
        job.Source,
        job.CreatedAt,
        job.UpdatedAt);
}
