using JobBoard.Api.Common;
using JobBoard.Api.Controllers;
using JobBoard.Domain.Entities;
using JobBoard.Domain.Enums;
using JobBoard.Identity;
using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Features.Imports;

/// <summary>
/// Imports jobs from free external feeds (Remotive, Jobicy) into the marketplace.
///
/// Rules:
/// - Idempotent: <c>(source, source_id)</c> is unique, so re-running only adds what is new.
/// - Imported jobs are published directly (they are curated public feeds, not user
///   submissions) and carry an attribution link back to the original posting.
/// - Each external company gets its own employer profile on a locked "import"
///   account, so listings show the real company name. Those accounts are never
///   e-mail verified and therefore cannot log in.
/// </summary>
public sealed class ExternalJobImporter(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IEnumerable<IExternalJobSource> sources,
    ILogger<ExternalJobImporter> logger)
{
    private const string ImportEmailDomain = "imports.jobboard.local";

    public async Task<ImportSummary> ImportAsync(int limitPerSource, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var results = new List<ImportSourceResult>();
        var categories = await db.Categories.Where(c => c.IsActive).ToListAsync(cancellationToken);
        if (categories.Count == 0)
            throw new InvalidOperationException("No categories exist — run the seeder first.");

        foreach (var source in sources)
        {
            var fetched = 0;
            var created = 0;
            var skipped = 0;
            var failed = 0;
            string? error = null;

            try
            {
                var jobs = await source.FetchAsync(limitPerSource, cancellationToken);
                fetched = jobs.Count;
                logger.LogInformation("Importing {Count} jobs from {Source}.", jobs.Count, source.Name);

                foreach (var raw in jobs)
                {
                    try
                    {
                        if (await db.Jobs.AnyAsync(j => j.Source == raw.Source && j.SourceId == raw.SourceId, cancellationToken))
                        {
                            skipped++;
                            continue;
                        }

                        var draft = ExternalJobMapper.Map(raw, now);
                        var employer = await ResolveEmployerAsync(draft.Company, cancellationToken);
                        var category = categories.FirstOrDefault(c => c.Slug == draft.CategorySlug) ?? categories[0];

                        var job = new Job
                        {
                            Id = Guid.NewGuid(),
                            EmployerProfileId = employer.Id,
                            CategoryId = category.Id,
                            Title = draft.Title,
                            Slug = await ProfilesController.UniqueSlugAsync(db, "j", draft.Title, cancellationToken),
                            Description = draft.Description,
                            JobType = draft.JobType,
                            Region = Region.Worldwide,
                            PayType = draft.PayType,
                            PayMin = draft.PayMin,
                            PayMax = draft.PayMax,
                            Currency = draft.Currency,
                            ExperienceLevel = draft.ExperienceLevel,
                            HoursPerWeek = null,
                            Status = JobStatus.Published,
                            PublishedAt = draft.PublishedAt,
                            Source = draft.Source,
                            SourceId = draft.SourceId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        };

                        foreach (var skill in draft.Skills)
                        {
                            job.Skills.Add(new JobSkill { Id = Guid.NewGuid(), SkillName = skill });
                        }

                        db.Jobs.Add(job);
                        created++;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        failed++;
                        logger.LogWarning(ex, "Skipped a {Source} job ({Id}).", source.Name, raw.SourceId);
                    }
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = ex.Message;
                failed += Math.Max(fetched, 1);
                logger.LogWarning(ex, "Import from {Source} failed.", source.Name);
            }

            results.Add(new ImportSourceResult(source.Name, fetched, created, skipped, failed, error));
        }

        var summary = new ImportSummary(
            results.Sum(r => r.Created),
            results.Sum(r => r.Skipped),
            results.Sum(r => r.Failed),
            results);

        logger.LogInformation("Import finished: {Created} created, {Skipped} already present, {Failed} failed.", summary.Created, summary.Skipped, summary.Failed);
        return summary;
    }

    /// <summary>
    /// One employer profile per company, owned by a locked system account
    /// (employer_profiles.user_id is one-to-one).
    /// </summary>
    private async Task<EmployerProfile> ResolveEmployerAsync(string company, CancellationToken cancellationToken)
    {
        var normalized = company.Trim();
        var existing = await db.EmployerProfiles
            .FirstOrDefaultAsync(p => p.CompanyName == normalized, cancellationToken);
        if (existing is not null)
            return existing;

        var email = $"company-{SlugHelper.Slugify(normalized)}@{ImportEmailDomain}";
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                FullName = normalized,
                // Unverified on purpose: these accounts can never complete login.
                EmailConfirmed = false,
                AccountStatus = AccountStatus.Active,
                PrivacyConsentAt = DateTime.UtcNow,
                PrivacyPolicyVersion = "2026-09-01",
                CreatedAt = DateTime.UtcNow,
            };

            var created = await userManager.CreateAsync(user, Guid.NewGuid().ToString("N") + "aA1!");
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the import account for '{normalized}': {string.Join(", ", created.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, Roles.Employer);
        }

        var profile = new EmployerProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CompanyName = normalized,
            Slug = await ProfilesController.UniqueSlugAsync(db, "c", normalized, cancellationToken),
            Description = "Company profile imported from a public job feed. Verify details on the employer's own site.",
            Country = null,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.EmployerProfiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return profile;
    }
}
