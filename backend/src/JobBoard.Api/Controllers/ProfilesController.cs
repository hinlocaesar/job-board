using JobBoard.Api.Common;
using JobBoard.Api.Features.Profiles;
using JobBoard.Domain.Entities;
using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Controllers;

[Route("api/profiles")]
public sealed class ProfilesController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public ProfilesController(AppDbContext db) => _db = db;

    // ---------- worker ----------

    /// <summary>Create the signed-in worker's profile.</summary>
    [HttpPost("worker")]
    [Authorize(Roles = Roles.Worker)]
    public async Task<ActionResult<ProfileMessage>> CreateWorker([FromBody] WorkerProfileInput input, CancellationToken cancellationToken)
    {
        await ValidateAsync(input);

        var userId = CurrentUserId;
        if (await _db.WorkerProfiles.AnyAsync(p => p.UserId == userId, cancellationToken))
            throw ApiException.Conflict("profile_exists", "You already have a worker profile.");

        var profile = new WorkerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Slug = await UniqueSlugAsync(_db, "w", input.Headline, cancellationToken),
        };
        ApplyWorkerInput(profile, input);

        _db.WorkerProfiles.Add(profile);
        await SyncSkillsAsync(profile, input, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetWorker), new { slug = profile.Slug }, new ProfileMessage(true, profile.Slug));
    }

    /// <summary>Update the signed-in worker's profile (including the public/private toggle).</summary>
    [HttpPut("worker")]
    [Authorize(Roles = Roles.Worker)]
    public async Task<ActionResult<ProfileMessage>> UpdateWorker([FromBody] WorkerProfileInput input, CancellationToken cancellationToken)
    {
        await ValidateAsync(input);

        var userId = CurrentUserId;
        var profile = await _db.WorkerProfiles
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw ApiException.NotFound("Worker profile");

        ApplyWorkerInput(profile, input);
        await SyncSkillsAsync(profile, input, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ProfileMessage(false, profile.Slug));
    }

    /// <summary>Public worker page. Private profiles 404 for everyone but the owner (or an admin).</summary>
    [HttpGet("worker/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<WorkerProfileDto>> GetWorker(string slug, CancellationToken cancellationToken)
    {
        var profile = await _db.WorkerProfiles
            .Include(p => p.Skills).ThenInclude(s => s.Skill)
            .Include(p => p.Experiences)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken)
            ?? throw ApiException.NotFound("Worker profile");

        var callerId = User.Identity?.IsAuthenticated == true ? CurrentUserId : (Guid?)null;
        var isAdmin = User.IsInRole(Roles.Admin);
        var isOwner = callerId is not null && callerId == profile.UserId;

        if (!profile.IsPublic && !isOwner && !isAdmin)
            throw ApiException.NotFound("Worker profile");

        var contactEmail = (string?)null;
        var contactPhone = (string?)null;
        if (callerId is not null && profile.IsPublic) // authenticated callers only
        {
            var user = await _db.Users.FindAsync([profile.UserId], cancellationToken);
            contactEmail = user?.Email;
            contactPhone = user?.PhoneNumber;
        }

        return Ok(ToDto(profile, contactEmail, contactPhone));
    }

    /// <summary>My own worker profile, regardless of visibility.</summary>
    [HttpGet("worker/me/current")]
    [Authorize(Roles = Roles.Worker)]
    public async Task<ActionResult<WorkerProfileDto>> GetMyWorkerProfile(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var profile = await _db.WorkerProfiles
            .Include(p => p.Skills).ThenInclude(s => s.Skill)
            .Include(p => p.Experiences)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw ApiException.NotFound("Worker profile");

        var user = await _db.Users.FindAsync([userId], cancellationToken);
        return Ok(ToDto(profile, user?.Email, user?.PhoneNumber));
    }

    // ---------- employer ----------

    [HttpPost("employer")]
    [Authorize(Roles = Roles.Employer)]
    public async Task<ActionResult<ProfileMessage>> CreateEmployer([FromBody] EmployerProfileInput input, CancellationToken cancellationToken)
    {
        await ValidateAsync(input);

        var userId = CurrentUserId;
        if (await _db.EmployerProfiles.AnyAsync(p => p.UserId == userId, cancellationToken))
            throw ApiException.Conflict("profile_exists", "You already have an employer profile.");

        var profile = new EmployerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Slug = await UniqueSlugAsync(_db, "c", input.CompanyName, cancellationToken),
        };
        ApplyEmployerInput(profile, input);

        _db.EmployerProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetEmployer), new { slug = profile.Slug }, new ProfileMessage(true, profile.Slug));
    }

    [HttpPut("employer")]
    [Authorize(Roles = Roles.Employer)]
    public async Task<ActionResult<ProfileMessage>> UpdateEmployer([FromBody] EmployerProfileInput input, CancellationToken cancellationToken)
    {
        await ValidateAsync(input);

        var userId = CurrentUserId;
        var profile = await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw ApiException.NotFound("Employer profile");

        ApplyEmployerInput(profile, input);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new ProfileMessage(false, profile.Slug));
    }

    [HttpGet("employer/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<EmployerProfileDto>> GetEmployer(string slug, CancellationToken cancellationToken)
    {
        var profile = await _db.EmployerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken)
            ?? throw ApiException.NotFound("Employer profile");

        return Ok(ToDto(profile));
    }

    [HttpGet("employer/me/current")]
    [Authorize(Roles = Roles.Employer)]
    public async Task<ActionResult<EmployerProfileDto>> GetMyEmployerProfile(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        var profile = await _db.EmployerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw ApiException.NotFound("Employer profile");

        return Ok(ToDto(profile));
    }

    // ---------- mapping / helpers ----------

    private static void ApplyWorkerInput(WorkerProfile profile, WorkerProfileInput input)
    {
        profile.Headline = input.Headline.Trim();
        profile.Summary = input.Summary?.Trim();
        profile.Country = input.Country.Trim().ToUpperInvariant();
        profile.City = input.City?.Trim();
        profile.TimeZone = input.TimeZone?.Trim();
        profile.YearsOfExperience = input.YearsOfExperience;
        profile.RateMin = input.RateMin;
        profile.RateMax = input.RateMax;
        profile.Currency = input.Currency.Trim().ToUpperInvariant();
        profile.RatePeriod = EnumValue<Domain.Enums.RatePeriod>.Parse(input.RatePeriod, nameof(input.RatePeriod));
        profile.Availability = EnumValue<Domain.Enums.Availability>.Parse(input.Availability, nameof(input.Availability));
        profile.IsPublic = input.IsPublic;
        profile.ResumeFileId = input.ResumeFileId;
        profile.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyEmployerInput(EmployerProfile profile, EmployerProfileInput input)
    {
        profile.CompanyName = input.CompanyName.Trim();
        profile.Website = input.Website?.Trim();
        profile.Description = input.Description?.Trim();
        profile.Country = string.IsNullOrWhiteSpace(input.Country) ? null : input.Country.Trim().ToUpperInvariant();
        profile.LogoFileId = input.LogoFileId;
        profile.UpdatedAt = DateTime.UtcNow;
    }

    private async Task SyncSkillsAsync(WorkerProfile profile, WorkerProfileInput input, CancellationToken cancellationToken)
    {
        var existing = profile.Skills.ToList();
        _db.WorkerSkills.RemoveRange(existing);

        foreach (var skillInput in input.Skills.GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
        {
            var name = skillInput.Name.Trim();
            var skill = await _db.Skills.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
            if (skill is null)
            {
                skill = new Skill { Id = Guid.NewGuid(), Name = name };
                _db.Skills.Add(skill);
            }

            _db.WorkerSkills.Add(new WorkerSkill
            {
                Id = Guid.NewGuid(),
                WorkerProfileId = profile.Id,
                SkillId = skill.Id,
                Level = skillInput.Level,
                YearsExperience = skillInput.YearsExperience,
            });
        }

        // Experience blocks are replaced wholesale for predictability.
        _db.WorkExperiences.RemoveRange(profile.Experiences.ToList());
        var order = 0;
        foreach (var experience in input.Experiences)
        {
            _db.WorkExperiences.Add(new WorkExperience
            {
                Id = Guid.NewGuid(),
                WorkerProfileId = profile.Id,
                Title = experience.Title.Trim(),
                CompanyName = experience.CompanyName.Trim(),
                Location = experience.Location?.Trim(),
                StartDate = experience.StartDate,
                EndDate = experience.EndDate,
                IsCurrent = experience.IsCurrent,
                Description = experience.Description?.Trim(),
                SortOrder = experience.SortOrder != 0 ? experience.SortOrder : order,
            });
            order++;
        }
    }

    private static WorkerProfileDto ToDto(WorkerProfile profile, string? contactEmail, string? contactPhone) => new(
        profile.Id,
        profile.Slug,
        profile.Headline,
        profile.Summary,
        profile.Country,
        profile.City,
        profile.TimeZone,
        profile.YearsOfExperience,
        profile.RateMin,
        profile.RateMax,
        profile.Currency,
        profile.RatePeriod.ToString(),
        profile.Availability.ToString(),
        profile.IsPublic,
        profile.ResumeFileId,
        profile.Skills.Select(s => new WorkerSkillDto(s.Skill?.Name ?? string.Empty, s.Level, s.YearsExperience)).ToList(),
        profile.Experiences.OrderBy(e => e.SortOrder).Select(e => new ExperienceDto(
            e.Id, e.Title, e.CompanyName, e.Location, e.StartDate, e.EndDate, e.IsCurrent, e.Description, e.SortOrder)).ToList(),
        contactEmail,
        contactPhone,
        profile.CreatedAt,
        profile.UpdatedAt);

    private static EmployerProfileDto ToDto(EmployerProfile profile) => new(
        profile.Id,
        profile.Slug,
        profile.CompanyName,
        profile.Website,
        profile.Description,
        profile.Country,
        profile.LogoFileId,
        profile.IsVerified,
        profile.CreatedAt,
        profile.UpdatedAt);

    internal static async Task<string> UniqueSlugAsync(AppDbContext db, string prefix, string source, CancellationToken cancellationToken)
    {
        var baseSlug = SlugHelper.Slugify(source);
        var candidate = $"{prefix}-{baseSlug}";
        var suffix = 1;
        while (await db.WorkerProfiles.AnyAsync(p => p.Slug == candidate, cancellationToken)
            || await db.EmployerProfiles.AnyAsync(p => p.Slug == candidate, cancellationToken)
            || await db.Jobs.AnyAsync(j => j.Slug == candidate, cancellationToken))
        {
            suffix++;
            candidate = $"{prefix}-{baseSlug}-{suffix}";
        }

        return candidate;
    }
}
