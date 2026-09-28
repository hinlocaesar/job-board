using System.Text.Json;
using JobBoard.Api.Common;
using JobBoard.Api.Features.Auth;
using JobBoard.Api.Services;
using JobBoard.Infrastructure.Data;
using JobBoard.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Controllers;

/// <summary>Account self-service: view, portable export and deletion (PH Data Privacy Act, in spirit).</summary>
[Route("api/me")]
[Authorize]
public sealed class MeController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public MeController(AppDbContext db, UserManager<ApplicationUser> userManager, IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
    }

    public sealed record MeDto(
        UserDto User,
        Guid? WorkerProfileId,
        Guid? EmployerProfileId,
        DateTime? PrivacyConsentAt,
        string? PrivacyPolicyVersion,
        bool MarketingConsent);

    public sealed record DeleteAccountInput(string Password);

    [HttpGet]
    public async Task<ActionResult<MeDto>> Me(CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId.ToString())
            ?? throw ApiException.NotFound("Account");

        var workerId = await _db.WorkerProfiles.Where(p => p.UserId == user.Id).Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var employerId = await _db.EmployerProfiles.Where(p => p.UserId == user.Id).Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new MeDto(
            new UserDto(user.Id, user.Email ?? string.Empty, user.FullName, roles.ToList(), user.EmailConfirmed, user.AccountStatus.ToString()),
            workerId,
            employerId,
            user.PrivacyConsentAt,
            user.PrivacyPolicyVersion,
            user.MarketingConsent));
    }

    /// <summary>Everything this account owns, as JSON — the right to data portability.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId.ToString())
            ?? throw ApiException.NotFound("Account");

        var workerProfile = await _db.WorkerProfiles.AsNoTracking()
            .Include(p => p.Skills).ThenInclude(s => s.Skill)
            .Include(p => p.Experiences)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

        var employerProfile = await _db.EmployerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

        var applications = await _db.Applications.AsNoTracking()
            .Where(a => a.WorkerProfile!.UserId == user.Id)
            .Include(a => a.Job)
            .AsSplitQuery()
            .Select(a => new { a.Id, a.JobId, JobTitle = a.Job!.Title, a.Status, a.AppliedAt, a.CoverLetter })
            .ToListAsync(cancellationToken);

        var jobs = await _db.Jobs.AsNoTracking()
            .Where(j => j.EmployerProfile!.UserId == user.Id)
            .Select(j => new { j.Id, j.Title, j.Slug, j.Status, j.CreatedAt })
            .ToListAsync(cancellationToken);

        var payload = new
        {
            exportedAt = DateTime.UtcNow,
            account = new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                user.CreatedAt,
                user.PrivacyConsentAt,
                user.PrivacyPolicyVersion,
                user.MarketingConsent,
                roles = await _userManager.GetRolesAsync(user),
            },
            workerProfile,
            employerProfile,
            applications,
            jobs,
            files = await _db.StoredFiles.AsNoTracking()
                .Where(f => f.OwnerUserId == user.Id)
                .Select(f => new { f.Id, f.OriginalFileName, f.SizeBytes, f.CreatedAt })
                .ToListAsync(cancellationToken),
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"jobboard-export-{user.Id:N}.json");
    }

    /// <summary>
    /// Delete the account: PII is anonymized (not silently kept), sessions are revoked
    /// and the row is retained only as an anonymized tombstone.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] DeleteAccountInput input, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(CurrentUserId.ToString())
            ?? throw ApiException.NotFound("Account");

        if (!await _userManager.CheckPasswordAsync(user, input.Password))
            throw ApiException.Unauthorized("invalid_password", "Password is incorrect.");

        var tombstone = $"deleted-{user.Id:N}";
        user.UserName = $"{tombstone}@removed.invalid";
        user.Email = $"{tombstone}@removed.invalid";
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.NormalizedEmail = user.Email.ToUpperInvariant();
        user.FullName = null;
        user.PhoneNumber = null;
        user.EmailConfirmed = false;
        user.AccountStatus = Domain.Enums.AccountStatus.Deleted;
        user.PrivacyConsentAt = null;
        user.PrivacyPolicyVersion = null;
        user.MarketingConsent = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var worker = await _db.WorkerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
        if (worker is not null)
        {
            worker.Headline = "Deleted profile";
            worker.Summary = null;
            worker.City = null;
            worker.Slug = $"{tombstone}-worker";
            worker.IsPublic = false;
            worker.ResumeFileId = null;
            worker.UpdatedAt = DateTime.UtcNow;
        }

        var employer = await _db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
        if (employer is not null)
        {
            employer.CompanyName = "Deleted company";
            employer.Website = null;
            employer.Description = null;
            employer.Slug = $"{tombstone}-company";
            employer.LogoFileId = null;
            employer.UpdatedAt = DateTime.UtcNow;
        }

        var tokens = await _db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(cancellationToken);
        tokens.ForEach(t => t.RevokedAt = DateTime.UtcNow);

        var codes = await _db.AuthCodes.Where(c => c.UserId == user.Id && c.UsedAt == null).ToListAsync(cancellationToken);
        _db.AuthCodes.RemoveRange(codes);

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync("user.deleted", "user", user.Id.ToString(), null, user.Id, ClientIp, cancellationToken);

        return NoContent();
    }
}
