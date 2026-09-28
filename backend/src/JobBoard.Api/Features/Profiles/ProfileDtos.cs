using System.Text.Json.Serialization;

namespace JobBoard.Api.Features.Profiles;

public sealed record SkillInput(string Name, byte Level = 3, short YearsExperience = 0);

public sealed record ExperienceInput(
    string Title,
    string CompanyName,
    string? Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Description,
    int SortOrder = 0);

public sealed record WorkerProfileInput(
    string Headline,
    string? Summary,
    string Country,
    string? City,
    string? TimeZone,
    short YearsOfExperience,
    decimal RateMin,
    decimal RateMax,
    string Currency,
    string RatePeriod,
    string Availability,
    bool IsPublic,
    Guid? ResumeFileId,
    IReadOnlyList<SkillInput> Skills,
    IReadOnlyList<ExperienceInput> Experiences);

public sealed record WorkerSkillDto(string Name, byte Level, short YearsExperience);
public sealed record ExperienceDto(Guid Id, string Title, string CompanyName, string? Location, DateOnly StartDate, DateOnly? EndDate, bool IsCurrent, string? Description, int SortOrder);

public sealed record WorkerProfileDto(
    Guid Id,
    string Slug,
    string Headline,
    string? Summary,
    string Country,
    string? City,
    string? TimeZone,
    short YearsOfExperience,
    decimal RateMin,
    decimal RateMax,
    string Currency,
    string RatePeriod,
    string Availability,
    bool IsPublic,
    Guid? ResumeFileId,
    IReadOnlyList<WorkerSkillDto> Skills,
    IReadOnlyList<ExperienceDto> Experiences,
    /// <summary>Only populated for authenticated callers — hidden from anonymous visitors.</summary>
    string? ContactEmail,
    string? ContactPhone,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record EmployerProfileInput(
    string CompanyName,
    string? Website,
    string? Description,
    string? Country,
    Guid? LogoFileId);

public sealed record EmployerProfileDto(
    Guid Id,
    string Slug,
    string CompanyName,
    string? Website,
    string? Description,
    string? Country,
    Guid? LogoFileId,
    bool IsVerified,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ProfileMessage(bool Created, string Slug);
