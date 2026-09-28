using JobBoard.Domain.Enums;

namespace JobBoard.Api.Features.Imports;

/// <summary>A job as published by an external feed, before it is mapped to our model.</summary>
public sealed record RawExternalJob(
    string Source,
    string SourceId,
    string Title,
    string Company,
    string? DescriptionHtml,
    string? Category,
    IReadOnlyList<string> Tags,
    string? JobType,
    string? ExperienceLevel,
    string? Salary,
    string? Url,
    DateTime? PublishedAt);

/// <summary>A mapped job, ready to be persisted. Produced by <see cref="ExternalJobMapper"/>.</summary>
public sealed record ImportedJobDraft(
    string Source,
    string SourceId,
    string Title,
    string Company,
    string Description,
    string CategorySlug,
    JobType JobType,
    ExperienceLevel ExperienceLevel,
    PayType PayType,
    decimal PayMin,
    decimal PayMax,
    string Currency,
    IReadOnlyList<string> Skills,
    string? SourceUrl,
    DateTime PublishedAt);

/// <summary>Per-source statistics of one import run.</summary>
public sealed record ImportSourceResult(string Source, int Fetched, int Created, int Skipped, int Failed, string? Error);

/// <summary>Aggregated result returned by the import endpoint.</summary>
public sealed record ImportSummary(
    int Created,
    int Skipped,
    int Failed,
    IReadOnlyList<ImportSourceResult> Sources);
