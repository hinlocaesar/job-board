using JobBoard.Domain.Entities;
using JobBoard.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<EmployerProfile> EmployerProfiles => Set<EmployerProfile>();
    public DbSet<WorkerProfile> WorkerProfiles => Set<WorkerProfile>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<WorkerSkill> WorkerSkills => Set<WorkerSkill>();
    public DbSet<WorkExperience> WorkExperiences => Set<WorkExperience>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AuthCode> AuthCodes => Set<AuthCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Table/column names are lower_snake_case via UseSnakeCaseNamingConvention()
        // registered on the options builder (see DependencyInjection).

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.AccountStatus).HasConversion<int>();
            e.HasIndex(u => u.NormalizedEmail);
        });

        builder.Entity<EmployerProfile>(e =>
        {
            e.ToTable("employer_profiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(220).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne<ApplicationUser>().WithOne().HasForeignKey<EmployerProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.LogoFile).WithMany().HasForeignKey(x => x.LogoFileId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.LogoFileId).HasColumnName("logo_file_id");
        });

        builder.Entity<WorkerProfile>(e =>
        {
            e.ToTable("worker_profiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(220).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Headline).HasMaxLength(200).IsRequired();
            e.Property(x => x.RateMin).HasColumnType("numeric(12,2)");
            e.Property(x => x.RateMax).HasColumnType("numeric(12,2)");
            e.Property(x => x.Availability).HasConversion<int>();
            e.Property(x => x.RatePeriod).HasConversion<int>();
            e.HasOne<ApplicationUser>().WithOne().HasForeignKey<WorkerProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ResumeFile).WithMany().HasForeignKey(x => x.ResumeFileId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.IsPublic);
        });

        builder.Entity<Skill>(e =>
        {
            e.ToTable("skills");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<WorkerSkill>(e =>
        {
            e.ToTable("worker_skills");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.WorkerProfile).WithMany(p => p.Skills).HasForeignKey(x => x.WorkerProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Skill).WithMany(s => s.WorkerSkills).HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.WorkerProfileId, x.SkillId }).IsUnique();
        });

        builder.Entity<WorkExperience>(e =>
        {
            e.ToTable("work_experiences");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.WorkerProfile).WithMany(p => p.Experiences).HasForeignKey(x => x.WorkerProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasOne(x => x.Parent).WithMany(c => c.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Job>(e =>
        {
            e.ToTable("jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(220).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.PayMin).HasColumnType("numeric(12,2)");
            e.Property(x => x.PayMax).HasColumnType("numeric(12,2)");
            e.Property(x => x.JobType).HasConversion<int>();
            e.Property(x => x.Region).HasConversion<int>();
            e.Property(x => x.PayType).HasConversion<int>();
            e.Property(x => x.ExperienceLevel).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne(x => x.EmployerProfile).WithMany(p => p.Jobs).HasForeignKey(x => x.EmployerProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Category).WithMany(c => c.Jobs).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.Status, x.PublishedAt });
        });

        builder.Entity<JobSkill>(e =>
        {
            e.ToTable("job_skills");
            e.HasKey(x => x.Id);
            e.Property(x => x.SkillName).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.SkillName);
            e.HasOne(x => x.Job).WithMany(j => j.Skills).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Application>(e =>
        {
            e.ToTable("applications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.JobId, x.WorkerProfileId }).IsUnique();
            e.HasOne(x => x.Job).WithMany(j => j.Applications).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.WorkerProfile).WithMany(p => p.Applications).HasForeignKey(x => x.WorkerProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ResumeFile).WithMany().HasForeignKey(x => x.ResumeFileId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StoredFile>(e =>
        {
            e.ToTable("stored_files");
            e.HasKey(x => x.Id);
            e.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
            e.Property(x => x.Purpose).HasConversion<int>();
            e.HasIndex(x => x.StorageKey).IsUnique();
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Action).HasMaxLength(120).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.CreatedAt);
        });

        builder.Entity<AuthCode>(e =>
        {
            e.ToTable("auth_codes");
            e.HasKey(x => x.Id);
            e.Property(x => x.CodeHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.Purpose).HasConversion<int>();
            e.HasIndex(x => new { x.UserId, x.Purpose });
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
