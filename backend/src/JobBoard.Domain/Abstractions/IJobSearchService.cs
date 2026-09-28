using JobBoard.Domain.Enums;

namespace JobBoard.Domain.Abstractions;

/// <summary>Filters for the public job board.</summary>
public sealed record JobSearchCriteria
{
    public string? Query { get; init; }
    public Guid? CategoryId { get; init; }
    public JobType? JobType { get; init; }
    public ExperienceLevel? ExperienceLevel { get; init; }
    /// <summary>Pay floor; matches jobs whose top-of-range reaches it (same <see cref="PayType"/>).</summary>
    public decimal? MinPay { get; init; }
    public PayType? PayType { get; init; }
    public string? Currency { get; init; }
    /// <summary>newest | pay_desc | relevance (relevance only when <see cref="Query"/> is set).</summary>
    public string Sort { get; init; } = "newest";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

public sealed record JobSearchItem(
    Guid Id,
    string Title,
    string Slug,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    string CompanyName,
    string CompanySlug,
    JobType JobType,
    Region Region,
    PayType PayType,
    decimal PayMin,
    decimal PayMax,
    string Currency,
    ExperienceLevel ExperienceLevel,
    short? HoursPerWeek,
    DateTime PublishedAt,
    IReadOnlyList<string> Skills);

public sealed record JobSearchResult(
    IReadOnlyList<JobSearchItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Full-text search over Postgres. Behind an interface so it can be swapped for
/// Elasticsearch/Meilisearch later without touching callers (Prompt: NON-FUNCTIONAL).
/// </summary>
public interface IJobSearchService
{
    Task<JobSearchResult> SearchAsync(JobSearchCriteria criteria, CancellationToken cancellationToken = default);
}
