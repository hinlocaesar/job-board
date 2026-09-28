using System.Net.Http.Json;
using System.Text.Json;

namespace JobBoard.Api.Features.Imports;

/// <summary>A free, key-less remote-job feed the importer can read.</summary>
public interface IExternalJobSource
{
    /// <summary>Stable key stored in <c>jobs.source</c> (e.g. "remotive").</summary>
    string Name { get; }

    /// <summary>Human-readable name shown in the import summary.</summary>
    string DisplayName { get; }

    Task<IReadOnlyList<RawExternalJob>> FetchAsync(int limit, CancellationToken cancellationToken);
}

/// <summary>https://remotive.com/api/remote-jobs — remote jobs, no API key.</summary>
public sealed class RemotiveJobSource(HttpClient http, ILogger<RemotiveJobSource> logger) : IExternalJobSource
{
    public const string SourceName = "remotive";

    private const string Endpoint = "https://remotive.com/api/remote-jobs";

    public string Name => SourceName;

    public string DisplayName => "Remotive";

    public async Task<IReadOnlyList<RawExternalJob>> FetchAsync(int limit, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(new Uri($"{Endpoint}?limit={limit}"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Remotive returned {Status}.", (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var jobs = ParseJobs(payload);
        logger.LogInformation("Remotive returned {Count} jobs.", jobs.Count);
        return jobs;
    }

    /// <summary>Maps the API payload. Public so it can be unit-tested offline.</summary>
    public static IReadOnlyList<RawExternalJob> ParseJobs(JsonElement payload)
    {
        if (!payload.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            return [];

        var results = new List<RawExternalJob>();
        foreach (var job in jobs.EnumerateArray())
        {
            var id = JsonFields.Text(job, "id");
            var title = JsonFields.Text(job, "title");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                continue;

            results.Add(new RawExternalJob(
                Source: SourceName,
                SourceId: id,
                Title: title,
                Company: JsonFields.Text(job, "company_name") ?? string.Empty,
                DescriptionHtml: JsonFields.Text(job, "description"),
                Category: JsonFields.Text(job, "category"),
                Tags: JsonFields.Strings(job, "tags"),
                JobType: JsonFields.Text(job, "job_type"),
                ExperienceLevel: null,
                Salary: JsonFields.Text(job, "salary"),
                Url: JsonFields.Text(job, "url"),
                PublishedAt: ParseDate(job)));
        }

        return results;
    }

    public static DateTime? ParseDate(JsonElement job)
    {
        var value = JsonFields.Text(job, "publication_date");
        return DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }
}

/// <summary>https://jobicy.com/api/v2/remote-jobs — remote jobs, no API key.</summary>
public sealed class JobicyJobSource(HttpClient http, ILogger<JobicyJobSource> logger) : IExternalJobSource
{
    public const string SourceName = "jobicy";

    private const string Endpoint = "https://jobicy.com/api/v2/remote-jobs";

    public string Name => SourceName;

    public string DisplayName => "Jobicy";

    public async Task<IReadOnlyList<RawExternalJob>> FetchAsync(int limit, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(new Uri($"{Endpoint}?count={limit}"), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Jobicy returned {Status}.", (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var jobs = ParseJobs(payload);
        logger.LogInformation("Jobicy returned {Count} jobs.", jobs.Count);
        return jobs;
    }

    /// <summary>Maps the API payload. Public so it can be unit-tested offline.</summary>
    public static IReadOnlyList<RawExternalJob> ParseJobs(JsonElement payload)
    {
        if (!payload.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
            return [];

        var results = new List<RawExternalJob>();
        foreach (var job in jobs.EnumerateArray())
        {
            var id = JsonFields.Text(job, "id");
            var title = JsonFields.Text(job, "jobTitle");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                continue;

            var industry = JsonFields.Strings(job, "jobIndustry");
            var jobTypes = JsonFields.Strings(job, "jobType");

            results.Add(new RawExternalJob(
                Source: SourceName,
                SourceId: id,
                Title: title,
                Company: JsonFields.Text(job, "companyName") ?? string.Empty,
                DescriptionHtml: JsonFields.Text(job, "jobDescription"),
                Category: industry.Count > 0 ? string.Join(", ", industry) : null,
                Tags: industry,
                JobType: jobTypes.Count > 0 ? string.Join(", ", jobTypes) : null,
                ExperienceLevel: JsonFields.Text(job, "jobLevel"),
                Salary: Salary(job),
                Url: JsonFields.Text(job, "url"),
                PublishedAt: ParseDate(job)));
        }

        return results;
    }

    /// <summary>Jobicy publishes annual salary bounds when available; the marketplace shows hourly pay.</summary>
    public static string? Salary(JsonElement job)
    {
        var min = Number(job, "annualSalaryMin");
        var max = Number(job, "annualSalaryMax");
        if (min is null && max is null)
            return null;

        return max is null ? $"${min}" : $"${min} - ${max}";
    }

    private static decimal? Number(JsonElement job, string property) =>
        job.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : null;

    public static DateTime? ParseDate(JsonElement job)
    {
        var value = JsonFields.Text(job, "pubDate");
        return DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }
}

/// <summary>
/// Feeds are not consistent about JSON types: Remotive sends <c>tags</c> as an
/// array while other fields are delimited strings. Read defensively instead of
/// assuming a shape (an earlier version crashed on the array).
/// </summary>
public static class JsonFields
{
    /// <summary>Property as text. Arrays are joined; missing/unsupported values are null.</summary>
    public static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.Array => Strings(element, property).Count > 0 ? string.Join(", ", Strings(element, property)) : null,
            _ => null,
        };
    }

    /// <summary>Property as a list of non-empty strings (empty when absent).</summary>
    public static List<string> Strings(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return [];

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ToString())
                // Feeds pad tags with whitespace ("Typescript "); trim before use.
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToList();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return (value.GetString() ?? string.Empty)
                .Split([',', '|', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return [];
    }
}
