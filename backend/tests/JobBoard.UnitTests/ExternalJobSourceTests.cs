using System.Text.Json;
using FluentAssertions;
using JobBoard.Api.Features.Imports;
using Xunit;

namespace JobBoard.UnitTests;

/// <summary>
/// The feeds are inconsistent about JSON types, so the parsers must tolerate
/// string-or-array for the same field. Payloads below are copied from the live
/// APIs (Remotive sends <c>tags</c> as an array; Jobicy omits salary entirely).
/// </summary>
public class ExternalJobSourceTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Remotive_reads_tags_sent_as_a_json_array()
    {
        const string payload = """
        { "jobs": [ { "id": 2091141, "title": "Frontend Developer", "company_name": "KoboToolbox",
            "category": "Design", "tags": ["api", "django", "frontend", "Typescript "],
            "job_type": "full_time", "salary": "",
            "publication_date": "2026-09-18T16:43:22",
            "url": "https://remotive.com/remote-jobs/design/frontend-web-application-developer-2091141",
            "description": "<p>Build things.</p>" } ] }
        """;

        var jobs = RemotiveJobSource.ParseJobs(Parse(payload));

        jobs.Should().HaveCount(1);
        var job = jobs[0];
        job.Source.Should().Be(RemotiveJobSource.SourceName);
        job.SourceId.Should().Be("2091141");
        job.Title.Should().Be("Frontend Developer");
        job.Company.Should().Be("KoboToolbox");
        job.JobType.Should().Be("full_time");
        job.Tags.Should().BeEquivalentTo(["api", "django", "frontend", "Typescript"]);
        job.PublishedAt.Should().Be(new DateTime(2026, 9, 18, 16, 43, 22, DateTimeKind.Utc));
    }

    [Fact]
    public void Remotive_skips_entries_without_an_id_or_title()
    {
        const string payload = """
        { "jobs": [ { "id": 1, "title": "Valid" }, { "title": "No id" }, { "id": 3 } ] }
        """;

        var jobs = RemotiveJobSource.ParseJobs(Parse(payload));
        jobs.Should().HaveCount(1);
        jobs[0].Title.Should().Be("Valid");
    }

    [Fact]
    public void Remotive_survives_a_payload_without_a_jobs_array() =>
        RemotiveJobSource.ParseJobs(Parse("""{ "unexpected": true }""")).Should().BeEmpty();

    [Fact]
    public void Jobicy_reads_array_fields_and_salary_bounds()
    {
        const string payload = """
        { "jobs": [ { "id": 151890, "jobTitle": "Customer Success Manager", "companyName": "Canonical",
            "jobIndustry": ["Customer Support & Success"], "jobType": ["Full-Time"],
            "jobLevel": "Senior", "pubDate": "2026-09-20T10:00:00Z",
            "annualSalaryMin": 70000, "annualSalaryMax": 90000,
            "url": "https://jobicy.com/jobs/151890-csm", "jobDescription": "<p>Support</p>" } ] }
        """;

        var jobs = JobicyJobSource.ParseJobs(Parse(payload));

        jobs.Should().HaveCount(1);
        var job = jobs[0];
        job.Source.Should().Be(JobicyJobSource.SourceName);
        job.Tags.Should().BeEquivalentTo(["Customer Support & Success"]);
        job.JobType.Should().Be("Full-Time");
        job.ExperienceLevel.Should().Be("Senior");
        job.Category.Should().Be("Customer Support & Success");
        job.Salary.Should().Be("$70000 - $90000");
        job.PublishedAt.Should().Be(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Jobicy_leaves_salary_and_date_null_when_the_feed_omits_them()
    {
        const string payload = """
        { "jobs": [ { "id": 1, "jobTitle": "Writer", "companyName": "Acme" } ] }
        """;

        var job = JobicyJobSource.ParseJobs(Parse(payload))[0];
        job.Salary.Should().BeNull();
        job.PublishedAt.Should().BeNull();
        job.ExperienceLevel.Should().BeNull();
    }

    [Fact]
    public void Json_reader_handles_string_number_array_and_missing_values()
    {
        var element = Parse("""{ "s": "text", "n": 42, "a": ["x", "y"], "empty": [] }""");

        JsonFields.Text(element, "s").Should().Be("text");
        JsonFields.Text(element, "n").Should().Be("42");
        JsonFields.Text(element, "a").Should().Be("x, y");
        JsonFields.Text(element, "empty").Should().BeNull();
        JsonFields.Text(element, "missing").Should().BeNull();

        JsonFields.Strings(element, "a").Should().BeEquivalentTo(["x", "y"]);
        JsonFields.Strings(element, "missing").Should().BeEmpty();
    }

    [Fact]
    public void Json_reader_splits_a_delimited_string_into_a_list() =>
        JsonFields.Strings(Parse("""{ "tags": "vue, typescript | tailwind" }"""), "tags")
            .Should().BeEquivalentTo(["vue", "typescript", "tailwind"]);
}
