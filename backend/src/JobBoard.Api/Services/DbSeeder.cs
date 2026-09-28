using JobBoard.Api.Common;
using JobBoard.Domain.Entities;
using JobBoard.Domain.Enums;
using JobBoard.Identity;
using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Services;

/// <summary>
/// Idempotent startup seeding: roles, categories, and (Development only) demo accounts
/// and sample content so the UI always has something real to render.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await db.Database.MigrateAsync();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        await SeedCategoriesAsync(db);

        var seedDemo = environment.IsDevelopment() && config.GetValue("Seed:DemoUsers", true);
        if (!seedDemo)
        {
            logger.LogInformation("Demo user seeding disabled.");
            return;
        }

        var admin = await EnsureUserAsync(userManager, "admin@jobboard.local", "Admin123!", "Site Admin");
        await EnsureRoleAsync(userManager, admin, Roles.Admin);

        var employer = await EnsureUserAsync(userManager, "employer@jobboard.local", "Employer123!", "Ana Dela Cruz");
        await EnsureRoleAsync(userManager, employer, Roles.Employer);
        var employerProfile = await EnsureEmployerProfileAsync(db, employer);

        var worker = await EnsureUserAsync(userManager, "worker@jobboard.local", "Worker123!", "Maria Santos");
        await EnsureRoleAsync(userManager, worker, Roles.Worker);
        var workerProfile = await EnsureWorkerProfileAsync(db, worker);

        await SeedJobsAsync(db, employerProfile, workerProfile);

        await db.SaveChangesAsync();
        logger.LogInformation("Seeding complete. Demo logins: admin@jobboard.local, employer@jobboard.local, worker@jobboard.local");
    }

    private static async Task SeedCategoriesAsync(AppDbContext db)
    {
        if (await db.Categories.AnyAsync())
            return;

        var categories = new[]
        {
            ("Web Development", "web-development", "Frontend, backend and full-stack web roles."),
            ("Mobile Development", "mobile-development", "iOS, Android and cross-platform app work."),
            ("Graphic Design", "graphic-design", "Brand, logo, layout and visual design."),
            ("Writing & Content", "writing-content", "Copywriting, editing and content strategy."),
            ("Virtual Assistance", "virtual-assistance", "Admin, inbox and calendar support."),
            ("Customer Support", "customer-support", "Chat, email and ticketing support."),
            ("Digital Marketing", "digital-marketing", "SEO, paid ads, social and lifecycle."),
            ("Video & Animation", "video-animation", "Editing, motion graphics and explainer video."),
            ("Data & Analytics", "data-analytics", "Dashboards, analysis and reporting."),
            ("Accounting & Finance", "accounting-finance", "Bookkeeping, payroll and reporting."),
        };

        var sort = 0;
        foreach (var (name, slug, description) in categories)
        {
            db.Categories.Add(new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                Slug = slug,
                ShortDescription = description,
                SortOrder = sort++,
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedJobsAsync(AppDbContext db, EmployerProfile employer, WorkerProfile worker)
    {
        if (await db.Jobs.AnyAsync())
            return;

        var webDev = await db.Categories.FirstAsync(c => c.Slug == "web-development");
        var virtualAssist = await db.Categories.FirstAsync(c => c.Slug == "virtual-assistance");
        var design = await db.Categories.FirstAsync(c => c.Slug == "graphic-design");

        var published = new[]
        {
            new Job
            {
                Id = Guid.NewGuid(),
                EmployerProfileId = employer.Id,
                CategoryId = webDev.Id,
                Title = "Senior Vue.js Developer for SaaS dashboard",
                Slug = "j-senior-vuejs-developer-for-saas-dashboard",
                Description = """
                    We are rebuilding our analytics dashboard in **Vue 3 + TypeScript** and need a senior engineer who cares about performance and accessibility.

                    ## What you will do
                    - Ship features in a Vue 3 + Vite codebase
                    - Own complex data-visualisation screens
                    - Keep our component library tidy

                    ## Nice to have
                    - Experience with Pinia and TanStack Query
                    - Exposure to headless CMS content (Umbraco is a plus)
                    """,
                JobType = JobType.Contract,
                Region = Region.Worldwide,
                PayType = PayType.Hourly,
                PayMin = 35,
                PayMax = 55,
                Currency = "USD",
                ExperienceLevel = ExperienceLevel.Senior,
                HoursPerWeek = 30,
                Status = JobStatus.Published,
                PublishedAt = DateTime.UtcNow.AddDays(-2),
            },
            new Job
            {
                Id = Guid.NewGuid(),
                EmployerProfileId = employer.Id,
                CategoryId = virtualAssist.Id,
                Title = "Virtual Assistant for e-commerce operations",
                Slug = "j-virtual-assistant-for-e-commerce-operations",
                Description = """
                    Looking for a detail-oriented Virtual Assistant in the Philippines to support order processing, supplier emails and basic reporting (4 hours per day, overlapping US East Coast mornings).
                    """,
                JobType = JobType.PartTime,
                Region = Region.PhilippinesOnly,
                PayType = PayType.Hourly,
                PayMin = 8,
                PayMax = 12,
                Currency = "USD",
                ExperienceLevel = ExperienceLevel.Junior,
                HoursPerWeek = 20,
                Status = JobStatus.Published,
                PublishedAt = DateTime.UtcNow.AddDays(-6),
            },
        };

        foreach (var job in published)
            db.Jobs.Add(job);

        db.Jobs.Add(new Job
        {
            Id = Guid.NewGuid(),
            EmployerProfileId = employer.Id,
            CategoryId = design.Id,
            Title = "Graphic Designer for brand refresh (pending review)",
            Slug = "j-graphic-designer-for-brand-refresh-pending-review",
            Description = """
                A short, well-paid brand refresh project: new logo variants, social templates and a one-page brand sheet. This posting is waiting for admin approval on purpose so the moderation queue has real data.
                """,
            JobType = JobType.Freelance,
            Region = Region.Worldwide,
            PayType = PayType.FixedPrice,
            PayMin = 800,
            PayMax = 1500,
            Currency = "USD",
            ExperienceLevel = ExperienceLevel.Mid,
            Status = JobStatus.Pending,
        });

        db.JobSkills.Add(new JobSkill { Id = Guid.NewGuid(), JobId = published[0].Id, SkillName = "Vue.js" });
        db.JobSkills.Add(new JobSkill { Id = Guid.NewGuid(), JobId = published[0].Id, SkillName = "TypeScript" });
        db.JobSkills.Add(new JobSkill { Id = Guid.NewGuid(), JobId = published[0].Id, SkillName = "Tailwind CSS" });
        db.JobSkills.Add(new JobSkill { Id = Guid.NewGuid(), JobId = published[1].Id, SkillName = "Customer Support" });
        db.JobSkills.Add(new JobSkill { Id = Guid.NewGuid(), JobId = published[1].Id, SkillName = "Email Management" });

        // One application so the employer dashboard has a real applicant to shortlist.
        var existingApplication = await db.Applications.AnyAsync();
        if (!existingApplication)
        {
            db.Applications.Add(new Application
            {
                Id = Guid.NewGuid(),
                JobId = published[0].Id,
                WorkerProfileId = worker.Id,
                CoverLetter = "Hi! I have shipped three Vue 3 dashboards in the last two years and would love to help with yours.",
                ResumeFileId = worker.ResumeFileId,
                Status = ApplicationStatus.Submitted,
            });
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> users, string email, string password, string fullName)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true, // dev seed accounts skip the inbox
            AccountStatus = AccountStatus.Active,
            PrivacyConsentAt = DateTime.UtcNow,
            PrivacyPolicyVersion = "2026-09-01",
            CreatedAt = DateTime.UtcNow,
        };

        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not seed {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return user;
    }

    private static async Task EnsureRoleAsync(UserManager<ApplicationUser> users, ApplicationUser user, string role)
    {
        if (!await users.IsInRoleAsync(user, role))
            await users.AddToRoleAsync(user, role);
    }

    private static async Task<EmployerProfile> EnsureEmployerProfileAsync(AppDbContext db, ApplicationUser user)
    {
        var existing = await db.EmployerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (existing is not null)
            return existing;

        var profile = new EmployerProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CompanyName = "Acme Remote HQ",
            Slug = "c-acme-remote-hq",
            Website = "https://example.com",
            Description = "A remote-first company hiring Filipino talent since 2019.",
            Country = "PH",
            IsVerified = true,
        };
        db.EmployerProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    private static async Task<WorkerProfile> EnsureWorkerProfileAsync(AppDbContext db, ApplicationUser user)
    {
        var existing = await db.WorkerProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (existing is not null)
            return existing;

        var profile = new WorkerProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Slug = "w-maria-santos",
            Headline = "Senior Frontend Developer (Vue 3 / TypeScript)",
            Summary = "Six years building dashboards and design systems for SaaS teams in Cebu.",
            Country = "PH",
            City = "Cebu City",
            TimeZone = "Asia/Manila",
            YearsOfExperience = 6,
            RateMin = 30,
            RateMax = 45,
            Currency = "USD",
            RatePeriod = RatePeriod.Hour,
            Availability = Availability.Freelance,
            IsPublic = true,
        };
        db.WorkerProfiles.Add(profile);
        await db.SaveChangesAsync();

        foreach (var (name, level) in new[] { ("Vue.js", (byte)5), ("TypeScript", (byte)4), ("Tailwind CSS", (byte)4) })
        {
            var skill = await db.Skills.FirstOrDefaultAsync(s => s.Name == name)
                ?? db.Skills.Add(new Skill { Id = Guid.NewGuid(), Name = name }).Entity;

            db.WorkerSkills.Add(new WorkerSkill
            {
                Id = Guid.NewGuid(),
                WorkerProfileId = profile.Id,
                SkillId = skill.Id,
                Level = level,
                YearsExperience = 4,
            });
        }

        db.WorkExperiences.Add(new WorkExperience
        {
            Id = Guid.NewGuid(),
            WorkerProfileId = profile.Id,
            Title = "Frontend Developer",
            CompanyName = "SaaS Studio",
            Location = "Cebu City (remote)",
            StartDate = new DateOnly(2020, 3, 1),
            EndDate = null,
            IsCurrent = true,
            Description = "Own the design system and analytics screens.",
        });

        await db.SaveChangesAsync();
        return profile;
    }
}
